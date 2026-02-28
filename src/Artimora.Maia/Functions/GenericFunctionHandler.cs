using System.Collections.Concurrent;
using CopperDevs.Celesium;

namespace Artimora.Maia;

/// <summary>
/// Default <see cref="IFunctionHandler"/> implementation that routes function calls over Maia messages and resolves asynchronous call results.
/// </summary>
/// <param name="side">The local side this handler is attached to.</param>
public class GenericFunctionHandler(HandlerMetaData.Side side) : IFunctionHandler
{
    private Action<FunctionSenderData> messageSender = null!;

    private readonly ConcurrentDictionary<string, Func<Dictionary<string, string>, Dictionary<string, string>>> functions = [];

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Message>> returnQueue = [];

    private BaseInitializationOptions options = null!;

    /// <inheritdoc />
    public void SetOptions(BaseInitializationOptions newOptions) => options = newOptions;

    /// <inheritdoc />
    public void OnMessage(int client, Message message)
    {
        if (message.id == "artimora:function_call")
        {
            _ = Task.Run(() => HandleFunctionCall(client, message));
            return;
        }

        if (message.id == "artimora:function_results")
        {
            if (!message.GetValues().TryGetValue("artimora:function_return_id", out var rawReturnId) ||
                !Guid.TryParse(rawReturnId, out var returnId))
                return;

            if (returnQueue.TryRemove(returnId, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
    }

    /// <inheritdoc />
    public void RegisterMessageSender(Action<FunctionSenderData> sender) => messageSender = sender;

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args)
    {
        var payload = new Dictionary<string, string>(args);

        var targetClient = payload.TryGetValue("artimora:target_client", out var arg) &&
            int.TryParse(arg, out var parsedTargetClient)
            ? parsedTargetClient
            : -1;

        var id = Guid.NewGuid();

        payload["artimora:function_name"] = functionName;
        payload["artimora:function_return_id"] = id.ToString();

        var toSend = new Message("artimora:function_call");
        toSend.SetValues(payload);

        var targetSide = side == HandlerMetaData.Side.Client ? HandlerMetaData.Side.Server : HandlerMetaData.Side.Client;

        var tcs = new TaskCompletionSource<Message>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!returnQueue.TryAdd(id, tcs))
            throw new InvalidOperationException("Duplicate returnId");

        try
        {
            messageSender?.Invoke(new FunctionSenderData(targetSide, toSend, targetClient));
        }
        catch
        {
            returnQueue.TryRemove(id, out _);
            throw;
        }

        var timeout = TimeSpan.FromMilliseconds(options.FunctionTimeout);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        if (completed != tcs.Task)
        {
            returnQueue.TryRemove(id, out _);
            Log.Warn($"Function call {id} timed out after {timeout.TotalSeconds}s");
            return new Dictionary<string, string> { ["artimora:error"] = "timeout" };
        }

        var response = await tcs.Task;

        return response.GetValues();
    }

    /// <inheritdoc />
    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false)
    {
        if (!forceSet && !functions.TryAdd(functionName, func))
        {
            Log.Error($"Function '{functionName}' is already registered");
            return;
        }

        functions[functionName] = func;
    }

    private void HandleFunctionCall(int client, Message message)
    {
        var targetSide = side == HandlerMetaData.Side.Client ? HandlerMetaData.Side.Server : HandlerMetaData.Side.Client;

        if (!message.GetValues().TryGetValue("artimora:function_name", out var functionName) ||
            !message.GetValues().TryGetValue("artimora:function_return_id", out var functionReturnId))
            return;

        if (!functions.TryGetValue(functionName, out var func))
        {
            Log.Fatal($"Function '{functionName}' not found");

            var errorReturn = new Message("artimora:function_results")
            {
                ["artimora:function_name"] = functionName,
                ["artimora:function_return_id"] = functionReturnId,
                ["artimora:error"] = "not_found"
            };

            messageSender?.Invoke(new FunctionSenderData(targetSide, errorReturn, client));
            return;
        }

        Dictionary<string, string> results;
        try
        {
            results = func(message.GetValues());
        }
        catch (Exception ex)
        {
            Log.Error($"Function '{functionName}' failed: {ex.Message}");

            results = new Dictionary<string, string>
            {
                ["artimora:error"] = "handler_error"
            };
        }

        results["artimora:function_name"] = functionName;
        results["artimora:function_return_id"] = functionReturnId;
        if (!results.ContainsKey("artimora:error"))
            results["artimora:error"] = "none";

        var toSend = new Message("artimora:function_results");
        toSend.SetValues(results);

        messageSender?.Invoke(new FunctionSenderData(targetSide, toSend, client));
    }
}

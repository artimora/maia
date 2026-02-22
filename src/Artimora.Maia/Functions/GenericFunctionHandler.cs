using System.Collections.Concurrent;
using CopperDevs.Logger;

namespace Artimora.Maia;

public class GenericFunctionHandler(HandlerMetaData.Side side) : IFunctionHandler
{
    private Action<FunctionSenderData> messageSender = null!;

    private readonly Dictionary<string, Func<Dictionary<string, string>, Dictionary<string, string>>> functions = [];

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Message>> returnQueue = [];
    private readonly ConcurrentBag<Guid> cancelledTasks = [];

    private BaseInitializationOptions options = null!;
    public void SetOptions(BaseInitializationOptions newOptions) => options = newOptions;

    public void OnMessage(int client, Message message)
    {
        if (message.id == "artimora:function_call")
        {
            var functionName = message["artimora:function_name"];
            var functionReturnId = message["artimora:function_return_id"];

            var targetSide = side == HandlerMetaData.Side.Client ? HandlerMetaData.Side.Server : HandlerMetaData.Side.Client;

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

            var results = func(message.GetValues());
            results["artimora:function_name"] = functionName;
            results["artimora:function_return_id"] = functionReturnId;
            results["artimora:error"] = "none";

            var toSend = new Message("artimora:function_results");
            toSend.SetValues(results);

            messageSender?.Invoke(new FunctionSenderData(targetSide, toSend, client));
        }

        if (message.id == "artimora:function_results")
        {
            var returnId = Guid.Parse(message["artimora:function_return_id"]);

            if (cancelledTasks.Contains(returnId))
            {
                Log.Warn($"Function call {returnId} has finally arrived, but has already been cancelled");
                return;
            }

            if (returnQueue.TryRemove(returnId, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
    }

    public void RegisterMessageSender(Action<FunctionSenderData> sender) => messageSender = sender;

    public async Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args)
    {
        var targetClient = args.TryGetValue("artimora:target_client", out var arg)
            ? int.Parse(arg)
            : -1;

        var id = Guid.NewGuid(); // could check returnQueue and cancelledTasks to make sure we didn't already generate an id

        args["artimora:function_name"] = functionName;
        args["artimora:function_return_id"] = id.ToString();

        var toSend = new Message("artimora:function_call");
        toSend.SetValues(args);

        var targetSide = side == HandlerMetaData.Side.Client ? HandlerMetaData.Side.Server : HandlerMetaData.Side.Client;

        var tcs = new TaskCompletionSource<Message>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        messageSender?.Invoke(new FunctionSenderData(targetSide, toSend, targetClient));

        if (!returnQueue.TryAdd(id, tcs))
            throw new InvalidOperationException("Duplicate returnId");

        var timeout = TimeSpan.FromMilliseconds(options.FunctionTimeout);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        if (completed != tcs.Task)
        {
            returnQueue.TryRemove(id, out _);
            cancelledTasks.Add(id);
            Log.Warn($"Function call {id} timed out after {timeout.TotalSeconds}s");
            return new Dictionary<string, string> { ["artimora:error"] = "timeout" };
        }

        var response = await tcs.Task;

        return response.GetValues();
    }

    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false)
    {
        if (functions.ContainsKey(functionName) && !forceSet)
        {
            Log.Error($"Function '{functionName}' is already registered");
            return;
        }

        functions[functionName] = func;
    }
}
using CopperDevs.Logger;

namespace Artimora.Maia;

public class GenericFunctionHandler(HandlerMetaData.Side side) : IFunctionHandler
{
    private Action<FunctionSenderData> messageSender = null!;

    private readonly Dictionary<string, Func<Dictionary<string, string>, Dictionary<string, string>>> functions = [];

    public void OnMessage(int client, Message message)
    {
        if (message.id != "artimora.function_call")
            return;

        var functionName = message["artimora:function_name"];
        var functionReturnId = message["artimora:function_return_id"];

        var func = functions[functionName];

        var results = func(message.GetValues());
        results["artimora:function_name"] = functionName;
        results["artimora:function_return_id"] = functionReturnId;

        var toSend = new Message("artimora:function_results");
        toSend.SetValues(results);

        messageSender?.Invoke(new FunctionSenderData(side, toSend, client));
    }

    public void RegisterMessageSender(Action<FunctionSenderData> sender) => messageSender = sender;

    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args)
    {
        var targetClient = args.TryGetValue("artimora:target_client", out var arg)
            ? int.Parse(arg)
            : -1;

        args["artimora:function_name"] = functionName;
        args["artimora:function_return_id"] = Guid.NewGuid().ToString();

        var toSend = new Message("artimora:function_call");
        
        messageSender?.Invoke(new FunctionSenderData(side, toSend, targetClient));
        
        // TODO: now we need to get the message result back here, then return the values
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
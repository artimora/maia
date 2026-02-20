namespace Artimora.Maia;

public class Server<TLayer> where TLayer : NetworkLayer, new()
{
    private readonly NetworkLayer network = new TLayer();

    public Action<(int client, Message message)> OnMessage = null!;
    public Action<int> OnClientConnect = null!;
    public Action<int> OnClientDisconnect = null!;

    private readonly IFunctionHandler functions;

    public Server() : this(ServerInitializationOptions.Default)
    {
    }

    public Server(ServerInitializationOptions options)
    {
        network.StartServer(options);

        network.SetOnMessage((m) => OnMessage?.Invoke((m.client, Message.Deserialize(m.data))));
        network.SetOnConnection(m => OnClientConnect?.Invoke(m.clientId ?? -1));
        network.SetOnDisconnect(m => OnClientDisconnect?.Invoke(m.clientId ?? -1));

        functions = options.FunctionHandler;
        functions.RegisterMessageSender((m) =>
        {
            if (m.TargetSide == HandlerMetaData.Side.Server)
                SendToClient(m.TargetClient, m.MessageContents);
        });
        OnMessage += (data => functions.OnMessage(data.client, data.message));
    }

    public void SendToClient(int id, Message message) => network.SendToClient(id, message.Serialize());

    public void SendToAllClients(Message message) => network.Send(message.Serialize());

    public int[] GetClients() => network.GetClients();

    public void Stop() => network.Stop();

    public void Tick() => network.Tick();

    public Task<Dictionary<string, string>> CallFunction(string functionName, int targetClient, Dictionary<string, string> args)
    {
        args["artimora:target_client"] = targetClient.ToString(); // gross
        return functions.CallFunction(functionName, args);
    }

    public Task<Dictionary<string, string>> CallFunction(string functionName, int targetClient) => CallFunction(functionName, targetClient, new Dictionary<string, string>());

    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false) => functions.RegisterFunction(functionName, func, forceSet);
}
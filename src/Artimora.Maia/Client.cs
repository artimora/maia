namespace Artimora.Maia;

public class Client<TLayer> where TLayer : NetworkLayer, new()
{
    private readonly NetworkLayer network = new TLayer();

    public Action<Message> OnMessage = null!;
    public Action OnConnection = null!;
    public Action OnDisconnect = null!;

    private readonly IFunctionHandler functions;

    public Client() : this(ClientInitializationOptions.Default)
    {
    }

    public Client(ClientInitializationOptions options)
    {
        network.StartClient(options);

        network.SetOnMessage((m) => OnMessage?.Invoke(Message.Deserialize(m.Item2)));
        network.SetOnConnection(_ => OnConnection?.Invoke());
        network.SetOnDisconnect(_ => OnDisconnect?.Invoke());

        functions = options.FunctionHandler;
    }

    public void Send(Message message) => network.Send(message.Serialize());

    public void Stop() => network.Stop();
    public void Tick() => network.Tick();
    
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string>? args) => functions.CallFunction(functionName, args);

    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func) => functions.RegisterFunction(functionName, func);
}
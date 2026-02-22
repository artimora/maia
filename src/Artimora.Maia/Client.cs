using CopperDevs.Logger;

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

        network.SetOnMessage((m) => OnMessage?.Invoke(Message.Deserialize(m.data)));
        network.SetOnConnection(_ => OnConnection?.Invoke());
        network.SetOnDisconnect(_ => OnDisconnect?.Invoke());

        functions = options.FunctionHandler;
        functions.SetOptions(options);
        functions.RegisterMessageSender((m) =>
        {
            if (m.TargetSide == HandlerMetaData.Side.Server)
                Send(m.MessageContents);
        });
        OnMessage += (message => functions.OnMessage(-1, message));
    }

    public void Send(Message message) => network.Send(message.Serialize());

    public void Stop() => network.Stop();
    public void Tick() => network.Tick();

    public Task<Dictionary<string, string>> CallFunction(string functionName) => CallFunction(functionName, []);
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args) => functions.CallFunction(functionName, args);

    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false) => functions.RegisterFunction(functionName, func, forceSet);
}
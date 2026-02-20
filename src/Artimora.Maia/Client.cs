namespace Artimora.Maia;


public class Client<TLayer> where TLayer : NetworkLayer, new()
{
    private NetworkLayer network = new TLayer();

    public Action<Message> OnMessage = null!;
    public Action OnConnection = null!;
    public Action OnDisconnect = null!;

    public Client() : this(ClientInitializationOptions.Default)
    {
    }
    
    public Client(ClientInitializationOptions options)
    {
        network.StartClient(options);

        network.SetOnMessage((m) => OnMessage?.Invoke(Message.Deserialize(m.Item2)));
        network.SetOnConnection(_ => OnConnection?.Invoke());
        network.SetOnDisconnect(_ => OnDisconnect?.Invoke());
    }

    public void Send(Message message) => network.Send(message.Serialize());

    public void Stop() => network.Stop();
    public void Tick() => network.Tick();
}
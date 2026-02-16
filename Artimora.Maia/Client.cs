using Artimora.Maia.Layers;

namespace Artimora.Maia;

public record struct ClientInitializationOptions()
{
    public string Host = "127.0.0.1";
    public int Port = 8080;

    // TODO: these two fields are only relevant to the TCP layer :p
    public int MaxMessageSize = 2048;
    public int ProcessLimit = 100;

    public AutoReconnectOptions AutoReconnect;

    public record struct AutoReconnectOptions()
    {
        public int DelayMs = 2000;
        public int MaxAttempts = 10;
    }

    public static ClientInitializationOptions Default => new();
}

public class Client<TLayer> where TLayer : NetworkLayer, new()
{
    private NetworkLayer network = new TLayer();

    public Action<Message> OnMessage = null!;
    public Action OnConnection = null!;
    public Action OnDisconnect = null!;

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
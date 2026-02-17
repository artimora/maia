namespace Artimora.Maia;

public record struct ServerInitializationOptions()
{
    public int Port = 8080;
    
    public static ServerInitializationOptions Default => new();
}

public class Server<TLayer> where TLayer : NetworkLayer, new()
{
    private readonly NetworkLayer network = new TLayer();

    public Action<Tuple<int, Message>> OnMessage = null!;
    public Action<int> OnClientConnect = null!;
    public Action<int> OnClientDisconnect = null!;

    public Server(ServerInitializationOptions options)
    {
        network.StartServer(options);

        network.SetOnMessage((m) => OnMessage?.Invoke(new Tuple<int, Message>(m.Item1, Message.Deserialize(m.Item2))));
        network.SetOnConnection(m => OnClientConnect?.Invoke(m.clientId ?? -1));
        network.SetOnDisconnect(m => OnClientDisconnect?.Invoke(m.clientId ?? -1));
    }

    public void SendToClient(int id, Message message) => network.SendToClient(id, message.Serialize());

    public void SendToAllClients(Message message) => network.Send(message.Serialize());

    public int[] GetClients() => network.GetClients();

    public void Stop() => network.Stop();

    public void Tick() => network.Tick();
}
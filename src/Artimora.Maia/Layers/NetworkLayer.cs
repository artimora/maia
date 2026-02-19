namespace Artimora.Maia;

public enum NetworkLayerState
{
    Disconnected,
    Server = HandlerMetaData.Side.Server,
    Client = HandlerMetaData.Side.Client,
}

public readonly record struct HandlerMetaData(int? clientId, HandlerMetaData.Side side)
{
    public readonly Side side = side;
    public readonly int? clientId = clientId;

    public enum Side
    {
        Server = 1,
        Client = 2
    }
};

public abstract class NetworkLayer
{
    public abstract NetworkLayerState GetState();
    public abstract void Stop();
    public abstract void Send(byte[] data);
    public abstract void Tick();

    public abstract void StartServer(ServerInitializationOptions options);
    public abstract void StartClient(ClientInitializationOptions options);

    public abstract void SendToClient(int clientId, byte[] data);
    public abstract int[] GetClients();

    public abstract void SetOnMessage(Action<(int client, byte[] data)> handler);
    public abstract void SetOnConnection(Action<HandlerMetaData> handler);
    public abstract void SetOnDisconnect(Action<HandlerMetaData> handler);
}
using System.Collections.Concurrent;
using System.Reflection;
using Artimora.Maia.Telepathy;
using CopperDevs.Logger;
using TelepathyServer = Artimora.Maia.Telepathy.Server;
using TelepathyClient = Artimora.Maia.Telepathy.Client;

namespace Artimora.Maia.Layers;

// ReSharper disable once InconsistentNaming
public class TCPNetworkingLayer : NetworkLayer
{
    private NetworkLayerState state = NetworkLayerState.Disconnected;

    private Action<Tuple<int, byte[]>>? onMessage;
    private Action<HandlerMetaData>? onConnection;
    private Action<HandlerMetaData>? onDisconnect;

    private TelepathyServer? server = null;
    private ServerInitializationOptions? serverOptions = null;

    private TelepathyClient? client = null;
    private ClientInitializationOptions? clientOptions = null;

    public override void SetOnMessage(Action<Tuple<int, byte[]>> handler) => onMessage = handler;

    public override void SetOnConnection(Action<HandlerMetaData> handler) => onConnection = handler;

    public override void SetOnDisconnect(Action<HandlerMetaData> handler) => onDisconnect = handler;

    public override NetworkLayerState GetState() => state;

    public override void Tick()
    {
        switch (GetState())
        {
            case NetworkLayerState.Disconnected:
                Log.Error("trying to tick but not started anything :p");
                break;
            case NetworkLayerState.Server:
                server!.Tick(serverOptions!.Value.ProcessLimit);
                break;
            case NetworkLayerState.Client:
                client!.Tick(clientOptions!.Value.ProcessLimit);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void StartServer(ServerInitializationOptions options)
    {
        serverOptions = options;
        server = new TelepathyServer(options.MaxMessageSize)
        {
            OnConnected = (connectionId, _) => onConnection?.Invoke(new HandlerMetaData(connectionId, HandlerMetaData.Side.Server)),
            OnData = (connectionId, message) => onMessage?.Invoke(new Tuple<int, byte[]>(connectionId, message.Array!)),
            OnDisconnected = (connectionId) => onDisconnect?.Invoke(new HandlerMetaData(connectionId, HandlerMetaData.Side.Server))
        };

        server.Start(options.Port);

        state = NetworkLayerState.Server;
    }

    public override void StartClient(ClientInitializationOptions options)
    {
        clientOptions = options;
        client = new TelepathyClient(options.MaxMessageSize)
        {
            OnConnected = () => onConnection?.Invoke(new HandlerMetaData(-1, HandlerMetaData.Side.Client)),
            OnData = (message) => onMessage?.Invoke(new Tuple<int, byte[]>(-1, message.Array!)),
            OnDisconnected = () => onDisconnect?.Invoke(new HandlerMetaData(-1, HandlerMetaData.Side.Client))
        };

        client.Connect(options.Host, options.Port);
        
        state = NetworkLayerState.Client;
        
    }

    public override void Stop()
    {
        server?.Stop();
        client?.Disconnect();
    }

    public override void Send(byte[] data)
    {
        switch (GetState())
        {
            case NetworkLayerState.Disconnected:
                Log.Error("trying to send data but not started anything :p");
                break;
            case NetworkLayerState.Server:
                foreach (var clientId in GetClients())
                {
                    server!.Send(clientId, data);
                }

                break;
            case NetworkLayerState.Client:
                client!.Send(data);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void SendToClient(int clientId, byte[] data)
    {
        server!.Send(clientId, data);
    }

    public override int[] GetClients()
    {
        var clients = (ConcurrentDictionary<int, ConnectionState>)(typeof(TelepathyServer)
            .GetField("clients", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!);

        return clients.Keys.ToArray();
    }
}
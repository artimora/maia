using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Artimora.Maia;

// ReSharper disable once InconsistentNaming
public sealed class TCPNetworkingLayer : NetworkLayer
{
    private NetworkLayerState currentState = NetworkLayerState.Disconnected;

    // server
    private TcpListener? listener;
    private readonly Dictionary<int, TcpClient> clients = [];
    private readonly Dictionary<TcpClient, int> clientIds = [];
    private readonly Dictionary<TcpClient, byte[]> serverRecv = new(); // per-client stream buffer
    private readonly object serverSync = new();
    private int nextClientId = 1;

    // client
    private TcpClient? client;
    private ClientInitializationOptions clientOptions = ClientInitializationOptions.Default;
    private bool hasClientOptions;
    private bool shouldReconnect;
    private int reconnectAttempts;
    private DateTime? nextReconnectAtUtc;

    // generic
    private readonly List<byte[]> sendQueue = []; // store unframed

    // inbound "push up"
    private Action<(int client, byte[] data)>? onMessage;
    private Action<HandlerMetaData>? onConnection;
    private Action<HandlerMetaData>? onDisconnect;

    // client stream buffer
    private byte[] clientRecv = [];

    public override NetworkLayerState GetState() => currentState;

    public override void SetOnMessage(Action<(int client, byte[] data)> handler) => onMessage = handler;
    public override void SetOnConnection(Action<HandlerMetaData> handler) => onConnection = handler;
    public override void SetOnDisconnect(Action<HandlerMetaData> handler) => onDisconnect = handler;

    public override void StartServer(ServerInitializationOptions options)
    {
        if (currentState != NetworkLayerState.Disconnected)
            throw new InvalidOperationException("Cannot start server: already active.");

        listener = new TcpListener(IPAddress.Any, options.Port);
        listener.Start();
        currentState = NetworkLayerState.Server;

        // flush queued sends (unframed, frame later)
        FlushSendQueue();
    }

    public override void StartClient(ClientInitializationOptions options)
    {
        if (currentState != NetworkLayerState.Disconnected)
            throw new InvalidOperationException("Cannot start client: already active.");

        clientOptions = options;
        hasClientOptions = true;

        // Your C# options always has AutoReconnect struct; treat MaxAttempts <= 0 as "disabled"
        shouldReconnect = options.AutoReconnect.MaxAttempts > 0;
        reconnectAttempts = 0;
        nextReconnectAtUtc = null;

        ConnectClient(options);
    }

    public override void Send(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (currentState == NetworkLayerState.Disconnected)
        {
            sendQueue.Add(data); // store unframed
            return;
        }

        var framed = Frame(data);

        switch (currentState)
        {
            case NetworkLayerState.Server:
            {
                TcpClient[] snapshot;
                lock (serverSync)
                {
                    snapshot = clients.Values.ToArray();
                }

                foreach (var connectedClient in snapshot)
                {
                    if (!TryWrite(connectedClient, framed))
                    {
                        CloseServerClient(connectedClient);
                    }
                }

                return;
            }
            case NetworkLayerState.Client when client is not { Connected: true }:
                throw new InvalidOperationException("Cannot send: client socket not connected.");
            case NetworkLayerState.Client when TryWrite(client, framed):
                return;
            case NetworkLayerState.Client:
                CloseClientSocket(fireDisconnect: true);
                ScheduleReconnect();

                return;
            case NetworkLayerState.Disconnected:
            default:
                throw new InvalidOperationException("Cannot send.");
        }
    }

    public override void SendToClient(int clientId, byte[] data)
    {
        if (currentState != NetworkLayerState.Server)
            throw new InvalidOperationException("sendToClient only valid in server mode.");

        TcpClient targetClient;
        lock (serverSync)
        {
            if (!clients.TryGetValue(clientId, out var foundClient) || foundClient is null)
                throw new ArgumentOutOfRangeException(nameof(clientId), "Invalid client ID");

            targetClient = foundClient;
        }

        var framed = Frame(data);
        if (!TryWrite(targetClient, framed)) CloseServerClient(targetClient);
    }

    public override int[] GetClients()
    {
        lock (serverSync)
        {
            return clients.Keys.Order().ToArray();
        }
    }

    public override void Stop()
    {
        shouldReconnect = false;
        nextReconnectAtUtc = null;
        hasClientOptions = false;
        reconnectAttempts = 0;

        if (currentState == NetworkLayerState.Server)
        {
            TcpClient[] snapshot;
            lock (serverSync)
            {
                snapshot = clients.Values.ToArray();
            }

            foreach (var connectedClient in snapshot)
            {
                CloseServerClient(connectedClient, fireDisconnect: false);
            }

            lock (serverSync)
            {
                clients.Clear();
                clientIds.Clear();
                serverRecv.Clear();
                nextClientId = 1;
            }

            try
            {
                listener?.Stop();
            }
            catch
            {
                /* idgaf just shutup */
            }

            listener = null;
        }

        if (currentState == NetworkLayerState.Client)
        {
            CloseClientSocket(fireDisconnect: false);
        }

        currentState = NetworkLayerState.Disconnected;
        clientRecv = [];

        onMessage = null;
        onConnection = null;
        onDisconnect = null;
    }

    public override void Tick()
    {
        switch (currentState)
        {
            case NetworkLayerState.Server:
                TickServerAccept();
                TickServerRead();
                return;
            case NetworkLayerState.Client:
                TickClientRead();
                return;
            case NetworkLayerState.Disconnected:
            default:
                // disconnected: maybe reconnect
                TickReconnect();
                break;
        }
    }

    private void TickServerAccept()
    {
        if (listener == null) return;

        try
        {
            while (listener.Pending())
            {
                var socket = listener.AcceptTcpClient();
                socket.NoDelay = true;

                int clientId;
                lock (serverSync)
                {
                    clientId = nextClientId++;
                    clients[clientId] = socket;
                    clientIds[socket] = clientId;
                    serverRecv[socket] = [];
                }

                onConnection?.Invoke(new HandlerMetaData(clientId, HandlerMetaData.Side.Server));
            }
        }
        catch
        {
            // once again, idgaf. if the listener dies, higher-level code can Stop().
        }
    }

    private void TickServerRead()
    {
        KeyValuePair<int, TcpClient>[] snapshot;
        lock (serverSync)
        {
            snapshot = clients.ToArray();
        }

        foreach (var (clientId, targetClient) in snapshot)
        {
            if (!targetClient.Connected || IsRemoteDisconnected(targetClient))
            {
                CloseServerClient(targetClient);
                continue;
            }

            var stream = SafeGetStream(targetClient);
            if (stream == null)
            {
                CloseServerClient(targetClient);
                continue;
            }

            if (!stream.DataAvailable) continue;

            byte[] recv;
            lock (serverSync)
            {
                if (!serverRecv.TryGetValue(targetClient, out var existingRecv))
                    recv = [];
                else
                    recv = existingRecv;
            }

            if (!TryReadAvailable(stream, ref recv))
            {
                CloseServerClient(targetClient);
                continue;
            }

            try
            {
                recv = DrainFrames(
                    recv,
                    emit: payload => onMessage?.Invoke((clientId, payload)),
                    side: HandlerMetaData.Side.Server
                );
            }
            catch
            {
                CloseServerClient(targetClient);
                continue;
            }

            lock (serverSync)
            {
                if (serverRecv.ContainsKey(targetClient))
                    serverRecv[targetClient] = recv; // write back updated buffer
            }
        }
    }

    private void CloseServerClient(TcpClient targetClient, bool fireDisconnect = true)
    {
        int? clientId;
        lock (serverSync)
        {
            clientId = null;
            if (clientIds.TryGetValue(targetClient, out var mappedClientId))
            {
                clientId = mappedClientId;
                clients.Remove(mappedClientId);
                clientIds.Remove(targetClient);
            }

            serverRecv.Remove(targetClient);
        }

        if (fireDisconnect && clientId.HasValue)
            onDisconnect?.Invoke(new HandlerMetaData(clientId, HandlerMetaData.Side.Server));

        try
        {
            targetClient.Close();
        }
        catch
        {
            /* once again, idgaf */
        }

    }

    private void ConnectClient(ClientInitializationOptions options)
    {
        try
        {
            clientRecv = [];

            client = new TcpClient();
            client.NoDelay = true;

            // synchronous connect, but fast enough for local IPC! if it fails, we schedule reconnect
            client.Connect(options.Host, options.Port);

            currentState = NetworkLayerState.Client;
            reconnectAttempts = 0;
            nextReconnectAtUtc = null;

            onConnection?.Invoke(new HandlerMetaData(null, HandlerMetaData.Side.Client));

            FlushSendQueue();
        }
        catch
        {
            currentState = NetworkLayerState.Disconnected;
            CloseClientSocket(fireDisconnect: false);
            ScheduleReconnect();
        }
    }

    private void TickClientRead()
    {
        if (client is not { Connected: true })
        {
            // treat as closed
            CloseClientSocket(fireDisconnect: true);
            ScheduleReconnect();
            return;
        }

        if (IsRemoteDisconnected(client))
        {
            CloseClientSocket(fireDisconnect: true);
            ScheduleReconnect();
            return;
        }

        var stream = SafeGetStream(client);
        if (stream == null)
        {
            CloseClientSocket(fireDisconnect: true);
            ScheduleReconnect();
            return;
        }

        if (!stream.DataAvailable) return;

        if (!TryReadAvailable(stream, ref clientRecv))
        {
            CloseClientSocket(fireDisconnect: true);
            ScheduleReconnect();
            return;
        }

        // drain frames
        try
        {
            clientRecv = DrainFrames(
                clientRecv,
                emit: payload => onMessage?.Invoke((-1, payload)),
                side: HandlerMetaData.Side.Client
            );
        }
        catch
        {
            CloseClientSocket(fireDisconnect: true);
            ScheduleReconnect();
        }
    }

    private void CloseClientSocket(bool fireDisconnect)
    {
        if (fireDisconnect)
            onDisconnect?.Invoke(new HandlerMetaData(null, HandlerMetaData.Side.Client));

        try
        {
            client?.Close();
        }
        catch
        {
            /* natural selection */
        }

        client = null;

        currentState = NetworkLayerState.Disconnected;
        clientRecv = [];
    }

    private void TickReconnect()
    {
        if (!shouldReconnect) return;
        if (!hasClientOptions) return;
        if (nextReconnectAtUtc == null) return;
        if (DateTime.UtcNow < nextReconnectAtUtc.Value) return;

        // only reconnect if still disconnected
        if (currentState != NetworkLayerState.Disconnected) return;

        // attempt
        nextReconnectAtUtc = null;
        ConnectClient(clientOptions);
    }

    private void ScheduleReconnect()
    {
        if (!shouldReconnect) return;
        if (!hasClientOptions) return;
        if (nextReconnectAtUtc != null) return; // already scheduled
        if (currentState != NetworkLayerState.Disconnected) return;

        var cfg = clientOptions.AutoReconnect;

        var delayMs = Math.Max(0, cfg.DelayMs <= 0 ? 1000 : cfg.DelayMs);
        var maxAttempts = cfg.MaxAttempts;

        if (maxAttempts > 0 && reconnectAttempts >= maxAttempts)
            return;

        reconnectAttempts++;
        nextReconnectAtUtc = DateTime.UtcNow.AddMilliseconds(delayMs);
    }

    private static byte[] Frame(byte[] payload)
    {
        var len = payload.Length;
        var buf = new byte[4 + len];
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0, 4), (uint)len);
        Buffer.BlockCopy(payload, 0, buf, 4, len);
        return buf;
    }

    private static byte[] DrainFrames(
        byte[] buffer,
        Action<byte[]> emit,
        HandlerMetaData.Side side
    )
    {
        var offset = 0;

        while (buffer.Length - offset >= 4)
        {
            var msgLenU = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
            if (msgLenU > int.MaxValue) throw new InvalidOperationException("Invalid frame length.");

            var msgLen = (int)msgLenU;

            if (msgLen < 0) throw new InvalidOperationException("Invalid frame length.");

            var available = buffer.Length - offset - 4;
            if (available < msgLen) break; // wait for more

            var start = offset + 4;
            var end = start + msgLen;

            var payload = new byte[msgLen];
            Buffer.BlockCopy(buffer, start, payload, 0, msgLen);
            emit(payload);

            offset = end;
        }

        if (offset == 0) return buffer;

        var remaining = buffer.Length - offset;
        if (remaining <= 0) return [];

        var next = new byte[remaining];
        Buffer.BlockCopy(buffer, offset, next, 0, remaining);
        return next;
    }

    private static NetworkStream? SafeGetStream(TcpClient c)
    {
        try
        {
            return c.GetStream();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsRemoteDisconnected(TcpClient c)
    {
        try
        {
            var socket = c.Client;
            return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool TryWrite(TcpClient client, byte[] data)
    {
        try
        {
            if (!client.Connected) return false;
            var stream = client.GetStream();
            stream.Write(data, 0, data.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadAvailable(NetworkStream stream, ref byte[] recvBuffer)
    {
        try
        {
            var temp = new byte[8192];

            while (stream.DataAvailable)
            {
                var read = stream.Read(temp, 0, temp.Length);
                if (read <= 0) return false;

                recvBuffer = Concat(recvBuffer, temp, read);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Concat(byte[] a, byte[] b, int bCount)
    {
        if (bCount <= 0) return a;

        var aLen = a.Length;
        var combined = new byte[aLen + bCount];
        if (aLen > 0) Buffer.BlockCopy(a, 0, combined, 0, aLen);
        Buffer.BlockCopy(b, 0, combined, aLen, bCount);
        return combined;
    }

    private void FlushSendQueue()
    {
        if (sendQueue.Count == 0) return;

        var copy = sendQueue.ToArray();
        sendQueue.Clear();

        foreach (var data in copy)
            Send(data);
    }
}

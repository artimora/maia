using CopperDevs.Celesium;

namespace Artimora.Maia;

public class Server<TLayer> where TLayer : NetworkLayer, new()
{
    private readonly NetworkLayer network = new TLayer();

    public Action<(int client, Message message)> OnMessage = null!;
    public Action<int> OnClientConnect = null!;
    public Action<int> OnClientDisconnect = null!;

    private readonly IFunctionHandler functions;

    // highkey the main reason a Shutdown and shouldRun duo is used here is that i have zero idea how CancellationToken works
    private bool shouldRun = true;
    public void Shutdown() => shouldRun = false;

    private readonly int tickDelay = 100;

    private Guid?[] clientIdentities = [];

    public Guid?[] GetClientIdentities() => clientIdentities;

    private void RequestIdentities()
    {
        SendToAllClients(new Message("artimora:identity_request"));
    }

    public Server() : this(ServerInitializationOptions.Default)
    {
    }

    public Server(ServerInitializationOptions options)
    {
        OnClientConnect += _ => { AdjustIdentitiesCatalog(); };
        OnClientDisconnect += _ => { AdjustIdentitiesCatalog(); };
        OnMessage += m =>
        {
            if (m.message.id == "artimora:identity")
                clientIdentities[GetClients().IndexOf(m.client)] = Guid.Parse(m.message["id"]);
        };

        network.SetOnMessage((m) => OnMessage?.Invoke((m.client, Message.Deserialize(m.data))));
        network.SetOnConnection(m => OnClientConnect?.Invoke(m.clientId ?? -1));
        network.SetOnDisconnect(m => OnClientDisconnect?.Invoke(m.clientId ?? -1));

        network.StartServer(options);

        functions = options.FunctionHandler;
        functions.SetOptions(options);
        functions.RegisterMessageSender((m) =>
        {
            if (m.TargetSide == HandlerMetaData.Side.Client)
                SendToClient(m.TargetClient, m.MessageContents);
        });
        OnMessage += (data => functions.OnMessage(data.client, data.message));

        tickDelay = Math.Clamp(options.TickDelay, 0, int.MaxValue);

        return;

        void AdjustIdentitiesCatalog()
        {
            Task.BackgroundRun(async () =>
            {
                await Task.Delay(10); // delay moment
                
                Log.Debug($"length {GetClients().Length}");
                Array.Resize(ref clientIdentities, GetClients().Length);

                for (var j = 0; j < clientIdentities.Length; j++)
                {
                    clientIdentities[j] = null;
                }

                RequestIdentities();
            });
        }
    }

    public void SendToClient(int id, Message message) => network.SendToClient(id, message.Serialize());

    public void SendToAllClients(Message message) => network.Send(message.Serialize());

    public int[] GetClients() => network.GetClients();

    public async Task Listen()
    {
        while (shouldRun)
        {
            network.Tick();
            await Task.Delay(tickDelay);
        }

        network.Stop();
    }

    /// <summary>
    /// Calls a function on a specific connected client.
    /// </summary>
    /// <param name="functionName">The function name to invoke.</param>
    /// <param name="targetClient">The target client id.</param>
    /// <param name="args">Function arguments to send to the client.</param>
    /// <returns>
    /// A dictionary containing the returned values and an <c>artimora:error</c> status entry.
    /// </returns>
    /// <remarks>
    /// If <c>artimora:error</c> is anything other than <c>none</c>, function result entries may be missing.
    /// </remarks>
    public Task<Dictionary<string, string>> CallFunction(string functionName, int targetClient, Dictionary<string, string> args)
    {
        args["artimora:target_client"] = targetClient.ToString(); // gross
        return functions.CallFunction(functionName, args);
    }

    /// <summary>
    /// Calls a function on a specific connected client with no additional arguments.
    /// </summary>
    /// <param name="functionName">The function name to invoke.</param>
    /// <param name="targetClient">The target client id.</param>
    /// <returns>
    /// A dictionary containing the returned values and an <c>artimora:error</c> status entry.
    /// </returns>
    /// <remarks>
    /// If <c>artimora:error</c> is anything other than <c>none</c>, function result entries may be missing.
    /// </remarks>
    public Task<Dictionary<string, string>> CallFunction(string functionName, int targetClient) => CallFunction(functionName, targetClient, new Dictionary<string, string>());

    /// <summary>
    /// Registers a server-side function that can be invoked by connected clients.
    /// </summary>
    /// <param name="functionName">The function identifier exposed to clients.</param>
    /// <param name="func">The function implementation.</param>
    /// <param name="forceSet">
    /// If <see langword="true"/>, replaces an existing function with the same name.
    /// </param>
    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false) => functions.RegisterFunction(functionName, func, forceSet);
}
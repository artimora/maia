namespace Artimora.Maia;

public class Client<TLayer> where TLayer : NetworkLayer, new()
{
    private readonly NetworkLayer network = new TLayer();

    public Action<Message> OnMessage = null!;
    public Action OnConnection = null!;
    public Action OnDisconnect = null!;

    private readonly IFunctionHandler functions;

    // highkey the main reason a Shutdown and shouldRun duo is used here is that i have zero idea how CancellationToken works
    private bool shouldRun = true;
    public void Shutdown() => shouldRun = false;

    private readonly int tickDelay = 100;

    private Guid clientId = Guid.NewGuid();

    public Client() : this(ClientInitializationOptions.Default)
    {
    }

    public Client(ClientInitializationOptions options)
    {
        OnMessage += m =>
        {
            if (m.id == "artimora:identity_request")
                Send(new Message("artimora:identity")
                {
                    ["id"] = clientId.ToString()
                });
        };

        network.SetOnMessage((m) => OnMessage?.Invoke(Message.Deserialize(m.data)));
        network.SetOnConnection(_ => OnConnection?.Invoke());
        network.SetOnDisconnect(_ => OnDisconnect?.Invoke());

        network.StartClient(options);

        functions = options.FunctionHandler;
        functions.SetOptions(options);
        functions.RegisterMessageSender((m) =>
        {
            if (m.TargetSide == HandlerMetaData.Side.Server)
                Send(m.MessageContents);
        });
        OnMessage += message => functions.OnMessage(-1, message);

        tickDelay = Math.Clamp(options.TickDelay, 0, int.MaxValue);
    }

    public void Send(Message message) => network.Send(message.Serialize());

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
    /// Calls a function on the connected server with no arguments.
    /// </summary>
    /// <param name="functionName">The function name to invoke.</param>
    /// <returns>
    /// A dictionary containing the returned values and an <c>artimora:error</c> status entry.
    /// </returns>
    /// <remarks>
    /// If <c>artimora:error</c> is anything other than <c>none</c>, function result entries may be missing.
    /// </remarks>
    public Task<Dictionary<string, string>> CallFunction(string functionName) => CallFunction(functionName, []);

    /// <summary>
    /// Calls a function on the connected server.
    /// </summary>
    /// <param name="functionName">The function name to invoke.</param>
    /// <param name="args">Function arguments to send to the server.</param>
    /// <returns>
    /// A dictionary containing the returned values and an <c>artimora:error</c> status entry.
    /// </returns>
    /// <remarks>
    /// If <c>artimora:error</c> is anything other than <c>none</c>, function result entries may be missing.
    /// </remarks>
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args) => functions.CallFunction(functionName, args);

    /// <summary>
    /// Registers a client-side function that can be invoked by the server.
    /// </summary>
    /// <param name="functionName">The function identifier exposed to the server.</param>
    /// <param name="func">The function implementation.</param>
    /// <param name="forceSet">
    /// If <see langword="true"/>, replaces an existing function with the same name.
    /// </param>
    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false) => functions.RegisterFunction(functionName, func, forceSet);
}
namespace Artimora.Maia;

public record BaseInitializationOptions(HandlerMetaData.Side Side)
{
    public int Port = 8080;
    
    public int TickDelay = 100;

    /// <summary>
    /// Time in milliseconds to wait for a function call result before returning a timeout error.
    /// </summary>
    public int FunctionTimeout = 8000;

    /// <summary>
    /// Function handler implementation used for registering and dispatching remote function calls.
    /// </summary>
    public IFunctionHandler FunctionHandler = new GenericFunctionHandler(Side);
}

public record ClientInitializationOptions() : BaseInitializationOptions(HandlerMetaData.Side.Client)
{
    public string Host = "127.0.0.1";

    public AutoReconnectOptions AutoReconnect = default;

    public record struct AutoReconnectOptions()
    {
        public int DelayMs = 2000;
        public int MaxAttempts = 10;
    }

    public static ClientInitializationOptions Default => new();
}

public record ServerInitializationOptions() : BaseInitializationOptions(HandlerMetaData.Side.Server)
{
    public static ServerInitializationOptions Default => new();
}

namespace Artimora.Maia;

public record BaseInitializationOptions
{
    public int Port = 8080;
}

public record ClientInitializationOptions : BaseInitializationOptions
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

public record ServerInitializationOptions : BaseInitializationOptions
{
    public static ServerInitializationOptions Default => new();
}
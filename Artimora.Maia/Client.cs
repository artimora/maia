namespace Artimora.Maia;

public record struct ClientInitializationOptions()
{
    public string Host = "127.0.0.1";
    public int Port = 8080;
    public AutoReconnectOptions AutoReconnect;

    public record struct AutoReconnectOptions()
    {
        public int DelayMs = 2000;
        public int MaxAttempts = 10;
    }

    public static ClientInitializationOptions Default => new();
}

public class Client(ClientInitializationOptions options)
{
    public Action<Message> OnMessage = null!;
    public Action OnConnection = null!;
    public Action OnDisconnect = null!;

    public void Send(Message message)
    {
    }

    public void Stop()
    {
    }
}
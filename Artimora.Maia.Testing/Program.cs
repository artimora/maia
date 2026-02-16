namespace Artimora.Maia.Testing;

public static class Program
{
    public static void Main(string[] args)
    {
        switch (args[0])
        {
            case "server":
                ServerMain();
                break;
            case "client":
                ClientMain();
                break;
        }
    }

    private static void ServerMain()
    {
        var server = new Server();
    }

    private static void ClientMain()
    {
        var client = new Client(ClientInitializationOptions.Default);
    }
}
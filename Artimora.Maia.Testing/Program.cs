using System.Text.Json;
using Artimora.Maia.Layers;
using CopperDevs.Logger;

namespace Artimora.Maia.Testing;

public static class Program
{
    public static async Task Main(string[] args)
    {
        switch (args[0])
        {
            case "server":
                await ServerMain();
                break;
            case "client":
                await ClientMain();
                break;
        }
    }

    private static async Task ServerMain()
    {
        var server = new Server<TCPNetworkingLayer>(ServerInitializationOptions.Default);

        server.OnClientConnect += (id) => Log.Network($"{id} connected");
        server.OnMessage += (m) => Log.Network($"{m.Item1}: {m.Item2.id}");
        server.OnClientDisconnect += (id) => Log.Network($"{id} disconnected");

        Task.Run(() =>
        {
            while (true)
            {
                server.Tick();
                Task.Delay(100);
            }
        });

        await Task.Run(() =>
        {
            while (true)
            {
                server.SendToAllClients(new Message("testing:time")
                {
                    ["time"] = $"{DateTime.UtcNow.Millisecond}"
                });
                Task.Delay(2500);
            }
        });
    }

    private static async Task ClientMain()
    {
        var client = new Client<TCPNetworkingLayer>(ClientInitializationOptions.Default);

        client.OnConnection += () => Log.Network("Connected");
        client.OnMessage += (m) => Log.Network($"{m.id}");
        client.OnDisconnect += () => Log.Network("Disconnected");

        Task.Run(() =>
        {
            while (true)
            {
                client.Tick();
                Task.Delay(100);
            }
        });

        await Task.Run(() =>
        {
            while (true)
            {
                client.Send(new Message("testing:time")
                {
                    ["time"] = $"{DateTime.UtcNow.Millisecond}"
                });
                Task.Delay(2500);
            }
        });
    }
}
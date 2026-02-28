using CopperDevs.Celesium;
using Random = CopperDevs.Celesium.Random;

// ReSharper disable FunctionNeverReturns

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
        server.OnMessage += (m) => Log.Network($"{m.client}: {m.message.id}");
        server.OnClientDisconnect += (id) => Log.Network($"{id} disconnected");

        server.RegisterFunction("addition", (args =>
        {
            var time = Random.Range(250, 1500);
            Thread.Sleep(time); // testing timeouts

            Log.Debug($"sleep: {time}");

            var left = int.Parse(args["left"]);
            var right = int.Parse(args["right"]);

            var result = left + right;

            return new Dictionary<string, string>
            {
                ["result"] = $"{result}"
            };
        }));

        Task.BackgroundRun(server.Listen);

        await Task.Run(async () =>
        {
            while (true)
            {
                Log.Info(server.GetClients().Select(i => i.ToString()).ToArray().AddFirstItem("Clients"));
                Log.Info(server.GetClientIdentities().Select(i => i is null ? "null" : i.ToString()).ToArray().AddFirstItem($"Client Identities ({server.GetClientIdentities().Length})"));
                await Task.Delay(1000);
            }
        });
    }

    private static async Task ClientMain()
    {
        var client = new Client<TCPNetworkingLayer>(ClientInitializationOptions.Default with { FunctionTimeout = 1000 }); // short timeout for testing

        client.OnConnection += () => Log.Network("Connected");
        client.OnMessage += (m) => Log.Network($"{m.id}");
        client.OnDisconnect += () => Log.Network("Disconnected");

        Task.BackgroundRun(client.Listen);

        await Task.Run(async () =>
        {
            while (client.ShouldRun())
            {
                const int left = 1;
                const int right = 2;

                var results = await client.CallFunction("addition", new Dictionary<string, string>()
                {
                    ["left"] = $"{left}",
                    ["right"] = $"{right}"
                });

                if (results.TryGetValue("result", out var numericalResults))
                    Log.Debug($"{left} + {right} = {numericalResults}");
                else
                    Log.Error($"Couldn't get result. Error value: {results["artimora:error"]}");

                await Task.Delay(1000);
            }
        });
    }
}
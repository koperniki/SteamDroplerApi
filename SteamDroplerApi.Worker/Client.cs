using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;
using SteamDroplerApi.Worker.Logic;

namespace SteamDroplerApi.Worker;

public static class Client
{
    public static async Task Run(string workerId, CancellationTokenSource cts)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:7832/worker", options => options.Headers.Add("workerId", workerId))
            .Build();

        await connection.StartAsync();

        SteamMachine? machine = null;

        connection.On<int, Account, MainConfig>("Start", (serverRecordMod, account, mainConfig) =>
        {
            if (machine != null)
            {
                Log.Logger.Warning("Try to start already started machine");
                return;
            }

            var builder = new LoggerConfiguration();
            if (mainConfig.LogWorker)
            {
                builder.WriteTo.File($"./logs/{account.Name}.log.txt", rollingInterval: RollingInterval.Day);
            }

            Log.Logger = builder.CreateLogger();
            Log.Logger.Information("Start machine");
            var tracker = new AccountTracker(account, connection);

            machine = new SteamMachine(tracker, serverRecordMod, mainConfig);
            machine.Start(cts.Token);
        });
        
        connection.On<List<uint>>("AddApps", async (ids) =>
        {
            Log.Logger.Information("Try to add apps {ids}", ids);
            if (machine != null)
            {
                await machine.AddFreeLicenseApp(ids);
            }
        });

        connection.On<uint>("AddPackage", async (id) =>
        {
            Log.Logger.Information("Try to add package {id}", id);
            if (machine != null)
            {
                await machine.AddFreeLicensePackage(id);
            }
        });

        connection.Closed += async (_) =>
        {
            Log.Logger.Warning("Connection closed");
            if (machine != null)
            {
                Log.Logger.Information("Try to stop steam machine");
                await cts.CancelAsync();
                await machine.StopAsync();
            }
            
        };

        await Task.Delay(TimeSpan.FromDays(2), cts.Token);
        Log.Logger.Information("Try to stop signalr connection");
        await connection.StopAsync();
    }
}
// See https://aka.ms/new-console-template for more information

using Microsoft.AspNetCore.SignalR.Client;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;
using SteamDroplerApi.Worker;
using SteamDroplerApi.Worker.Logic;


var workerId = args.First();

var exitEvent = new ManualResetEvent(false);
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:7832/worker", options => options.Headers.Add("workerId", workerId))
    .WithAutomaticReconnect()
    .Build();

await connection.StartAsync();


SteamMachine? machine = null;

connection.On<int, Account, MainConfig>("Start", (serverRecordMod, account, mainConfig) =>
{
    var tracker = new AccountTracker(account, connection);
    
    machine = new SteamMachine(tracker, serverRecordMod, mainConfig);
    machine.Start();
});

connection.On("Exit", async () =>
{
    if (machine != null)
    {
        await machine.StopAsync();
    }
    exitEvent.Set();
});

connection.Closed += async (_) =>
{
    if (machine != null)
    {
        await machine.StopAsync();
    }
    exitEvent.Set();
};

exitEvent.WaitOne();
await connection.StopAsync();
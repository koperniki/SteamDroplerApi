using Microsoft.AspNetCore.SignalR.Client;
using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Worker;

public class AccountTracker(Account account, HubConnection connection)
{
    public Account Account { get; } = account;


    public async Task LoginWithError(string eMessage)
    {
        await SafeExecute(async () => await connection.InvokeAsync("LoginWithError", eMessage));
    }

    public async Task ExitWithError(string eMessage)
    {
        await SafeExecute(async () => await connection.InvokeAsync("ExitWithError", eMessage));
    }

    public async Task TokenExpired()
    {
        Account.RunConfig.Token = null;
        await Save();
    }

    public async Task LoggedIn(ulong clientSteamId, string refreshToken)
    {
        Account.RunConfig.SteamId = clientSteamId;
        Account.RunConfig.Token = refreshToken;
        await Save();
    }

    public async Task ResetLicensesToAdd()
    {
        Account.RunConfig.AppsToAdd.Clear();
        Account.RunConfig.PackagesToAdd.Clear();
        await Save();
    }

    public async Task Plays()
    {
        await SafeExecute(async () => await connection.InvokeAsync("Plays"));
    }

    public async Task ItemDropped(string resultItemJson)
    {
        await SafeExecute(async () => await connection.InvokeAsync("ItemDropped", resultItemJson));
    }

    public async Task UpdateOwnedApps(List<uint> appIds)
    {
        Account.RunConfig.OwnedApps = appIds;
        await Save();
    }

    public async Task Disconnected()
    {
        await SafeExecute(async () => await connection.InvokeAsync("Disconnected", Account));
    }

    private async Task Save()
    {
        await SafeExecute(async () => await connection.InvokeAsync("Save", Account));
    }

    private async Task SafeExecute(Func<Task> func)
    {
        try
        {
            await func();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
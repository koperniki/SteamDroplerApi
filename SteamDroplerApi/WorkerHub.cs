using Microsoft.AspNetCore.SignalR;
using SteamDroplerApi.Core;
using SteamDroplerApi.Core.Models;
using SteamDroplerApi.Core.Services;

namespace SteamDroplerApi;

public class WorkerHub(
    ILogger<WorkerHub> logger,
    WorkerService workerService,
    MainConfigService mainConfigService,
    AccountConfigService accountConfigService,
    DropService dropService)
    : Hub<IWorkerHub>
{
    private static int _serverRecordMod = 1;

    public override async Task OnConnectedAsync()
    {
        var workerId = GetWorkerId();
        if (workerId == null)
        {
            return;
        }

        await workerService.WorkerConnected(workerId, Context.ConnectionId);
        var account = await GetAccount();
        if (account != null)
        {
            await Task.Delay(1_000);
            logger.LogInformation("Running {account}", account.Name);
            await Clients.Client(Context.ConnectionId)
                .Start(GetServerRecordMod(), account, mainConfigService.MainConfig!);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var workerId = GetWorkerId();
        var account = await GetAccount();
        if (workerId != null && account != null)
        {
           logger.LogWarning("Account {account} disconnected. Try to restart", account?.Name);
            await workerService.Reload(workerId);
        }
    }

    public async Task Plays()
    {
        var account = await GetAccount();
        if (account != null)
        {
            logger.LogInformation("Account {account} idling...", account.Name);
            account.IsPlaying = true;
        }
    }

    public async Task ItemDropped(string resultItemJson)
    {
        var account = await GetAccount();
        if (account != null)
        {
            await dropService.StoreDropResult(account, resultItemJson);
        }
    }

    public async Task ExitWithError(string eMessage)
    {
        var account = await GetAccount();
        if (account != null)
        {
            account.RunConfig.ErrorReason = eMessage;
            account.RunConfig.LastErrorTime = DateTime.UtcNow;
            logger.LogWarning("Error for account {account}: {exception}", account.Name, eMessage);
            await Save(account);
            await workerService.Reload(GetWorkerId()!);
        }
    }

    public async Task LoginWithError(string eMessage)
    {
        var account = await GetAccount();
        if (account != null)
        {
            account.RunConfig.LoginErrorReason = eMessage;
            account.RunConfig.LastLoginErrorTime = DateTime.UtcNow;
            logger.LogWarning("Login error for account {account}: {exception}", account.Name, eMessage);
            await Save(account);
            await workerService.Reload(GetWorkerId()!);
        }
    }

    public async Task Disconnected()
    {
        var account = await GetAccount();
        if (account != null)
        {
            logger.LogInformation("Account {account} disconnected...try restart", account.Name);
            await Clients.Client(Context.ConnectionId).Stop();
            await workerService.Reload(GetWorkerId()!);
        }
    }
    
    public async Task Save(Account newAccountData)
    {
        var account = await GetAccount();
        if (account != null)
        {
            account.RunConfig.UpdateTime = DateTime.UtcNow;
            account.RunConfig.SteamId = newAccountData.RunConfig.SteamId;
            account.RunConfig.Token = newAccountData.RunConfig.Token;
            account.RunConfig.LastLoginErrorTime = newAccountData.RunConfig.LastLoginErrorTime;
            account.RunConfig.LoginErrorReason = newAccountData.RunConfig.LoginErrorReason;
            account.RunConfig.LastErrorTime = newAccountData.RunConfig.LastErrorTime;
            account.RunConfig.ErrorReason = newAccountData.RunConfig.ErrorReason;
            account.RunConfig.PackagesToAdd = newAccountData.RunConfig.PackagesToAdd;
            account.RunConfig.AppsToAdd = newAccountData.RunConfig.AppsToAdd;
            
            var appDiffs = account.RunConfig.OwnedApps.Any()
                ? account.RunConfig.OwnedApps.Except(newAccountData.RunConfig.OwnedApps).ToList()
                : new List<uint>();
            if (appDiffs.Any())
            {
                logger.LogInformation("Account {account} has new apps: [{apps}]", account.Name,
                    string.Join(";", appDiffs));
            }
            account.RunConfig.OwnedApps = newAccountData.RunConfig.OwnedApps;
            
            await accountConfigService.SaveRunAccountConfig(account);
        }
    }

    private string? GetWorkerId()
    {
        var workerHeader = Context.GetHttpContext()?.Request?.Headers?.Where(t => t.Key == "workerId").ToList();
        return workerHeader?.Any() == true ? workerHeader.First().Value.First() : null;
    }

    private async Task<Account?> GetAccount()
    {
        var workerId = GetWorkerId();
        if (workerId == null)
        {
            return null;
        }

        return await workerService.GetAccount(workerId);
    }

    private static int GetServerRecordMod()
    {
        _serverRecordMod += 1;
        if (_serverRecordMod < 1)
        {
            _serverRecordMod = 1;
        }

        return _serverRecordMod;
    }
}
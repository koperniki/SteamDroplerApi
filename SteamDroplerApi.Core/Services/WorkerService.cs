using System.Collections.Concurrent;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Core.Services;

public class WorkerService(
    ILogger<WorkerService> logger,
    AccountConfigService accountConfigService,
    MainConfigService mainConfigService)
{

    private readonly CancellationTokenSource _cts = new();
    private Task? _task;
    private readonly MainConfig? _mainConfig = mainConfigService.MainConfig;
    private readonly ConcurrentDictionary<string, AccountWorkerProxy> _dict = new();

    public Task StartAsync()
    {
        if (_mainConfig == null)
        {
            return Task.CompletedTask;
        }

        _task = RunWorkers(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        if (_task != null)
        {
            await _task;
        }
    }

    public Task<Account?> WorkerConnected(string workerId, string connectionId)
    {
        if (_dict.TryGetValue(workerId, out var proxy))
        {
            proxy.WorkerConnected(connectionId);
            return Task.FromResult<Account?>(proxy.Account);
        }

        return Task.FromResult<Account?>(null);
    }

    private async Task RunWorkers(CancellationToken token)
    {
        var accounts = accountConfigService.Accounts
            .Where(t => t.Config.IdleEnable)
            .OrderBy(t => t.RunConfig.UpdateTime).ToList();


        foreach (var account in accounts)
        {
            var proxy = new AccountWorkerProxy(account, _mainConfig!);
            _dict[proxy.ProxyId] = proxy;
        }

        logger.LogInformation("Will be idling [{count}] accounts", _dict.Count);

        while (!token.IsCancellationRequested)
        {
            foreach (var pair in _dict)
            {
                await pair.Value.Do();
            }

            await Task.Delay(1_000, token);
        }

        foreach (var pair in _dict)
        {
            await pair.Value.Kill();
        }
    }

    public async Task Reload(string workerId)
    {
        if (_dict.TryGetValue(workerId, out var proxy))
        {
            await proxy.Reload();
        }
    }
}
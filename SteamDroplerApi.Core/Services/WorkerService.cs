using System.Collections.Concurrent;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Core.Services;

public class WorkerService
{

    private readonly CancellationTokenSource _cts = new();
    private Task? _task;
    private MainConfig? _mainConfig;
    private readonly ConcurrentDictionary<string, AccountDomainWorkerProxy> _dict = new();
    private readonly ILogger<WorkerService> _logger;
    private readonly AccountConfigService _accountConfigService;
    private readonly MainConfigService _mainConfigService;

    public WorkerService(ILogger<WorkerService> logger,
        AccountConfigService accountConfigService,
        MainConfigService mainConfigService)
    {
        _logger = logger;
        _accountConfigService = accountConfigService;
        _mainConfigService = mainConfigService;
       
    }

    public Task StartAsync()
    {
        _mainConfig = _mainConfigService.MainConfig;
        if (_mainConfig == null)
        {
            return Task.CompletedTask;
        }

        _task = RunWorkers(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Worker try to stop");
        await _cts.CancelAsync();
        if (_task != null)
        {
            await _task;
        }
    }

    public Task WorkerConnected(string workerId, string connectionId)
    {
        if (_dict.TryGetValue(workerId, out var proxy))
        {
            proxy.WorkerConnected(connectionId);
        }
        return Task.CompletedTask;
    }

    public Task<Account?> GetAccount(string workerId)
    {
        if (_dict.TryGetValue(workerId, out var proxy))
        {
            return Task.FromResult<Account?>(proxy.Account);
        }
        return Task.FromResult<Account?>(null);
    }

    private async Task RunWorkers(CancellationToken token)
    {
        var accounts = _accountConfigService.Accounts
            .Where(t => t.Config.IdleEnable)
            .OrderBy(t => t.RunConfig.UpdateTime).ToList();


        foreach (var account in accounts)
        {
            var proxy = new AccountDomainWorkerProxy(account, _mainConfig!);
            _dict[proxy.ProxyId] = proxy;
        }

        _logger.LogInformation("Will be idling [{count}] accounts", _dict.Count);

        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var pair in _dict)
                {
                    await pair.Value.Do(token);
                }

                await Task.Delay(1_000, token);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        _logger.LogInformation("Start exiting");
        foreach (var pair in _dict)
        {
            await pair.Value.Kill();
            _logger.LogInformation("Stopped {account}", pair.Value.Account.Name);
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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Core.Services;

public class AccountConfigService
{
    private static readonly string AccountConfigPath = Path.Combine("Configs", "Accounts");
    private static readonly string AccountRunConfigPath = Path.Combine(AccountConfigPath, "Run");
    private readonly ILogger<AccountConfigService> _logger;
    private readonly AppSystemService _systemService;

    private readonly List<Account> _accounts;
    public IReadOnlyList<Account> Accounts => _accounts;

    public AccountConfigService(ILogger<AccountConfigService> logger, AppSystemService systemService)
    {
        _logger = logger;
        _systemService = systemService;
        _accounts = new List<Account>();
    }


    public async Task ReadAccounts()
    {
        var directory = new DirectoryInfo(AccountConfigPath);
        if (!directory.Exists)
        {
            _logger.LogError("Accounts config folder doesnt exist. Exit app");
            await _systemService.StopApplication();
        }

        var runDirectory = new DirectoryInfo(AccountRunConfigPath);
        if (!runDirectory.Exists)
        {
            runDirectory.Create();
        }

        var files = directory.GetFiles("*.json");


        foreach (var fileInfo in files)
        {
            try
            {
                var account = await ReadAccount(fileInfo);
                _accounts.Add(account);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error with reading account config: {config}", fileInfo.Name);
            }
        }
    }

    private async Task<Account> ReadAccount(FileInfo fileInfo)
    {
        var name = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var data = await File.ReadAllTextAsync(fileInfo.FullName);
        var account = JsonConvert.DeserializeObject<AccountConfig>(data)!;

        var runConfigFile = new FileInfo(Path.Combine(AccountRunConfigPath, fileInfo.Name));
        var runConfig = new AccountRunConfig();
        if (runConfigFile.Exists)
        {
            var runData = await File.ReadAllTextAsync(runConfigFile.FullName);
            runConfig = JsonConvert.DeserializeObject<AccountRunConfig>(runData) ?? new AccountRunConfig();
        }

        return new Account(name, account, runConfig);
    }

    public async Task SaveRunAccountConfig(Account account)
    {
        var runDirectory = new DirectoryInfo(AccountRunConfigPath);
        var file = Path.Combine(runDirectory.FullName, $"{account.Name}.json");
        await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(account.RunConfig, Formatting.Indented));
    }

    public async Task AddApps(List<uint> apps)
    {
        foreach (var account in _accounts)
        {
            account.RunConfig.AppsToAdd = account.RunConfig.AppsToAdd.Union(apps).ToList();
            await SaveRunAccountConfig(account);
        }
    }

    public async Task AddPackage(uint packageId)
    {
        foreach (var account in _accounts)
        {
            account.RunConfig.PackagesToAdd = account.RunConfig.PackagesToAdd.Union(new[] { packageId }).ToList();
            await SaveRunAccountConfig(account);
        }
    }
}
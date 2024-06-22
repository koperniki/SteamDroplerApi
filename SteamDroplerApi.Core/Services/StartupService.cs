using Microsoft.Extensions.Hosting;

namespace SteamDroplerApi.Core.Services;

public class StartupService : IHostedService
{
    private readonly MainConfigService _mainConfigService;
    private readonly AccountConfigService _accountConfigService;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly WorkerService _workerService;


    public StartupService(MainConfigService mainConfigService, AccountConfigService accountConfigService,
        GoogleSheetsService googleSheetsService, WorkerService workerService)
    {
        _mainConfigService = mainConfigService;
        _accountConfigService = accountConfigService;
        _googleSheetsService = googleSheetsService;
        _workerService = workerService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _mainConfigService.ReadMainConfig();
        await _accountConfigService.ReadAccounts();
        //await _googleSheetsService.StartAsync();
        await _workerService.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _workerService.StopAsync();
    }
}
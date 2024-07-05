using Microsoft.Extensions.Hosting;

namespace SteamDroplerApi.Core.Services;

public class StartupService : IHostedService
{
    private readonly ILogger<StartupService> _logger;
    private readonly MainConfigService _mainConfigService;
    private readonly AccountConfigService _accountConfigService;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly WorkerService _workerService;


    public StartupService(ILogger<StartupService> logger, MainConfigService mainConfigService,
        AccountConfigService accountConfigService,
        GoogleSheetsService googleSheetsService, WorkerService workerService)
    {
        _logger = logger;
        _mainConfigService = mainConfigService;
        _accountConfigService = accountConfigService;
        _googleSheetsService = googleSheetsService;
        _workerService = workerService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SteamDroplerApi by koperniki.");
        _logger.LogInformation("Api run on http://localhost:7832 . Use /swagger to see api calls.");
        _logger.LogInformation("Press [CTRL]+C to exit");
        
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
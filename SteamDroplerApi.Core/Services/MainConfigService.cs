using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SteamDroplerApi.Core.Configs;

namespace SteamDroplerApi.Core.Services;

public class MainConfigService
{
    public static readonly Version ActualVersion =  new Version(0, 2);
    private static readonly string ConfigPath = Path.Combine("Configs", "MainConfig.json");
    
    private readonly ILogger<MainConfigService> _logger;
    private readonly AppSystemService _systemService;

    public MainConfig? MainConfig { get; private set; }
    
    
    public MainConfigService(ILogger<MainConfigService> logger, AppSystemService systemService)
    {
        _logger = logger;
        _systemService = systemService;
    }

    public async Task ReadMainConfig()
    {
        if (File.Exists(ConfigPath))
        {
            var data = await File.ReadAllTextAsync(ConfigPath);

            try
            {
                MainConfig = JsonConvert.DeserializeObject<MainConfig>(data);
                _logger.LogInformation("MainConfig.json file loaded.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Invalid MainConfig.json file. Exit app");
                await _systemService.StopApplication();
            }
        }
        else
        {
            _logger.LogError("MainConfig.json file doesnt exist. Exit app");
            await _systemService.StopApplication();
        }
    }
    
}
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SteamDroplerApi.Core.Services;

public class AppSystemService
{
    public static bool NeedRestart { get; private set; }
    
    
    private readonly ILogger<AppSystemService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public AppSystemService(ILogger<AppSystemService> logger, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }


    public Task StopApplication()
    {
        _hostApplicationLifetime.StopApplication();
        return Task.CompletedTask;
    }

    public Task RestartApplication()
    {
        NeedRestart = true;
        _hostApplicationLifetime.StopApplication();
        return Task.CompletedTask;
    }
    
    
}
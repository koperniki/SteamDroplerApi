using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SteamDroplerApi.Core;
using SteamDroplerApi.Core.Services;

namespace SteamDroplerApi.Controllers;

[Controller]
[Route("/api/[controller]")]
public class LicensingController : Controller
{
    private readonly ILogger<LicensingController> _logger;
    private readonly IHubContext<WorkerHub, IWorkerHub> _hub;
    private readonly AccountConfigService _accountConfigService;

    public LicensingController(ILogger<LicensingController> logger, IHubContext<WorkerHub, IWorkerHub> hub,
        AccountConfigService accountConfigService)
    {
        _logger = logger;
        _hub = hub;
        _accountConfigService = accountConfigService;
    }

    [HttpPost("addApps")]
    public async Task AddApps(List<uint> apps)
    {
        await _accountConfigService.AddApps(apps);
        await _hub.Clients.All.AddApps(apps);
    }

    [HttpPost("addPackage")]
    public async Task AddPackage(uint packageId)
    {
        await _accountConfigService.AddPackage(packageId);
        await _hub.Clients.All.AddPackage(packageId);
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SteamDroplerApi.Core;
using SteamDroplerApi.Core.Models;
using SteamDroplerApi.Core.Services;

namespace SteamDroplerApi.Controllers;

[Controller]
[Route("/api/[controller]")]
public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;
    private readonly AccountConfigService _accountConfigService;

    public AccountController(ILogger<AccountController> logger,
        AccountConfigService accountConfigService)
    {
        _logger = logger;
        _accountConfigService = accountConfigService;
    }

    [HttpGet()]
    public Task<List<Account>> Get()
    {
        return Task.FromResult(_accountConfigService.Accounts.ToList());
    }
}
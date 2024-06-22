using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Core.Services;

public class DropService
{
    private readonly ILogger<DropService> _logger;

    public DropService(ILogger<DropService> logger)
    {
        _logger = logger;
    }


    public Task StoreDropResult(Account account, string dropResult)
    {
        _logger.LogTrace("Drop on account {account}: {drop}", account.Name, dropResult);
        return Task.CompletedTask;
    }
}
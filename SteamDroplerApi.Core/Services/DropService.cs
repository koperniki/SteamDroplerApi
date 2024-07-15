using Newtonsoft.Json;
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
        try
        {
            var items = JsonConvert.DeserializeObject<List<DropResult>>(dropResult);
            if (items != null)
            {
                foreach (var item in items)
                {
                    _logger.LogInformation("Drop on account {account}: appId {appId} itemDefId {itemDefId}", account.Name,
                        item.AppId, item.ItemDefId);
                }
            }
            
        }
        catch
        {
            //
        }
     
        _logger.LogTrace("Drop on account {account}: {drop}", account.Name, dropResult);
        return Task.CompletedTask;
    }
}
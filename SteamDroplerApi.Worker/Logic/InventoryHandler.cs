using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic;

public class InventoryHandler
{
    private readonly AccountTracker _accountTracker;
    private readonly SteamClient _client;
    private readonly MainConfig _mainConfig;
    private readonly SteamUnifiedMessages.UnifiedService<IEcon> _econService;

    public InventoryHandler(AccountTracker accountTracker, SteamClient client, MainConfig mainConfig)
    {
        _accountTracker = accountTracker;
        _client = client;
        _mainConfig = mainConfig;
        var steamUnifiedMessages = client.GetHandler<SteamUnifiedMessages>()!;
        _econService = steamUnifiedMessages.CreateService<IEcon>();
    }


    public async Task<InventoryResponse> GetItems(uint appId, uint contextId = 2, bool tradableOnly = false,
        bool marketableOnly = false)
    {
        var invResponse = new InventoryResponse
        {
            IsValid = false
        };
        CEcon_GetInventoryItemsWithDescriptions_Request request = new()
        {
            appid = appId,
            contextid = contextId,
            filters = new CEcon_GetInventoryItemsWithDescriptions_Request.FilterOptions
            {
                tradable_only = tradableOnly,
                marketable_only = marketableOnly
            },
            get_descriptions = true,
            steamid = _client.SteamID!,
            count = 1000
        };
        var items = new Dictionary<ulong, InventoryItem>();
        var descriptions = new Dictionary<(ulong ClassID, ulong InstanceID), ItemDescription>();

        while (true)
        {
            SteamUnifiedMessages.ServiceMethodResponse? serviceMethodResponse = null;
            for (byte i = 0; i < 5; i++)
            {
                try
                {
                    serviceMethodResponse = await _econService
                        .SendMessage(x => x.GetInventoryItemsWithDescriptions(request)).ToLongRunningTask()
                        .ConfigureAwait(false);
                    if (serviceMethodResponse.Result != EResult.OK)
                    {
                        await Task.Delay(2_000);
                        continue;
                    }

                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e, "error until get items");
                    await Task.Delay(2_000);
                }
            }

            if (serviceMethodResponse == null)
            {
                return invResponse;
            }

            var response = serviceMethodResponse
                .GetDeserializedResponse<CEcon_GetInventoryItemsWithDescriptions_Response>();
            if (response.total_inventory_count == 0 || response.assets.Count == 0)
            {
                break;
            }

            foreach (var description in response.descriptions)
            {
                (ulong ClassID, ulong InstanceID) key = (description.classid, description.instanceid);

                if (descriptions.ContainsKey(key))
                {
                    continue;
                }

                descriptions.Add(key, new ItemDescription(description.classid, description.instanceid, description));
            }

            foreach (var asset in response.assets)
            {
                if (items.ContainsKey(asset.assetid))
                {
                    continue;
                }

                items[asset.assetid] = new InventoryItem(asset.assetid, asset.classid, asset.instanceid,
                    asset.instanceid, asset.amount);
            }

            if (!response.more_items)
            {
                break;
            }

            request.start_assetid = response.last_assetid;
        }

        invResponse.IsValid = true;
        return invResponse;
    }
}
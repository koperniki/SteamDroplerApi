using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic;

public class DropHandler
{
    private readonly AccountTracker _accountTracker;
    private readonly MainConfig _mainConfig;
    private readonly HashSet<uint> _skipGames;
    private readonly SteamUnifiedMessages.UnifiedService<IInventory> _inventoryService;
    private readonly Dictionary<uint, DateTime> _times;

    public DropHandler(AccountTracker accountTracker, SteamClient client, MainConfig mainConfig,
        HashSet<uint> skipGames)
    {
        _accountTracker = accountTracker;
        _mainConfig = mainConfig;
        _skipGames = skipGames;
        var steamUnifiedMessages = client.GetHandler<SteamUnifiedMessages>()!;
        _inventoryService = steamUnifiedMessages.CreateService<IInventory>();
        _times = new Dictionary<uint, DateTime>();
    }

    public async Task DropTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var dropConfig = GetNextGameConfigsToDrop(_mainConfig);

            if (dropConfig == null)
            {
                await Task.Delay(1000);
                continue;
            }

            foreach (var itemId in dropConfig.DropItemIds)
            {
                if (_skipGames.Contains(dropConfig.GameId))
                {
                    break;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await TryDropItem(token, dropConfig.GameId, itemId);
                    await Task.Delay(_mainConfig.IdDropCooldown * 1000, token);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error while DropTask");
                }
            }

            _times[dropConfig.GameId] = DateTime.UtcNow + TimeSpan.FromMinutes(dropConfig.CheckItemDropCooldown!.Value);
        }
    }


    private DropConfig? GetNextGameConfigsToDrop(MainConfig mainConfig)
    {
        var distinctConfig = mainConfig
            .DropConfig
            .GroupBy(t => t.GameId)
            .Select(t => new DropConfig()
            {
                GameId = t.Key,
                DropItemIds = t.SelectMany(x => x.DropItemIds).ToList(),
                CheckItemDropCooldown = t.Min(x => x.CheckItemDropCooldown) ?? mainConfig.CheckItemDropCooldown
            }).ToDictionary(t => t.GameId);


        foreach (var config in distinctConfig)
        {
            if (!_times.ContainsKey(config.Key))
            {
                _times[config.Key] = DateTime.MinValue;
            }
        }

        var now = DateTime.UtcNow;
        var possibleToCheckDrop = _times.Where(t => t.Value <= now).OrderBy(t => t.Value).FirstOrDefault();

        if (possibleToCheckDrop.Key != 0)
        {
            return distinctConfig[possibleToCheckDrop.Key];
        }

        return null;
    }

    private async Task TryDropItem(CancellationToken token, uint gameId, uint itemId)
    {
        var executed = false;
        do
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var reqkf = new CInventory_ConsumePlaytime_Request
            {
                appid = gameId,
                itemdefid = itemId
            };
            Log.Logger.Information("Sent CInventory_ConsumePlaytime_Request: gameId: {gameId} itemId: {itemId}",
                gameId, itemId);
            try
            {
                var response = await _inventoryService.SendMessage(x => x.ConsumePlaytime(reqkf))
                    .ToLongRunningTask();

                var result = response.GetDeserializedResponse<CInventory_Response>();
                if (response.Result == EResult.Fail && !string.IsNullOrEmpty(response.ErrorMessage) &&
                    response.ErrorMessage == "User must be allowed to play game to access inventory.")
                {
                    Log.Logger.Information("response result {resp} {result}", response.Result,
                        response.ErrorMessage);
                    _skipGames.Add(gameId);
                }

                if (result.item_json != "[]")
                {
                    Log.Logger.Information("ItemDropped: {result}", result.item_json);
                    await _accountTracker.ItemDropped(result.item_json);
                }

                executed = true;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Log.Logger.Error(e, "Error while sending CInventory_ConsumePlaytime_Request");
                Log.Logger.Information($"try again after wait {_mainConfig.IdDropErrorCooldown} sec");
                await Task.Delay(_mainConfig.IdDropErrorCooldown * 1000, token);
            }
        } while (!executed);
    }
}
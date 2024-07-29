using System.Collections;
using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamKit2;
using SteamKit2.Discovery;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic
{
    public class SteamMachine
    {
        private readonly AccountTracker _accountTracker;
        private readonly ServerRecord _serverRecord;
        private readonly MainConfig _mainConfig;
        private readonly SteamLoginHandler _loginHandler;
        private readonly SteamClient _client;
        private readonly SteamApps _steamApps;
        private readonly SteamUnifiedMessages.UnifiedService<IInventory> _inventoryService;
        private readonly SteamUnifiedMessages.UnifiedService<IPlayer> _playerService;
        private bool _work = true;
        private readonly CancellationTokenSource _tokenSource;
        private Task? _task;
        private SteamWebHandler? _steamWebHandler;
        private readonly List<uint> _skipGames;


        public SteamMachine(AccountTracker accountTracker, int serverRecordMod, MainConfig mainConfig)
        {
            _client = new SteamClient();
            /*var folder = $"D:\\logs\\{accountTracker.Account.Name}_{DateTime.Now.ToFileTime()}";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _client.DebugNetworkListener = new NetHookNetworkListener(folder);*/
            var records = (SteamDirectory.LoadAsync(_client.Configuration).Result)
                .Where(t => t.ProtocolTypes.HasFlag(ProtocolTypes.Tcp))
                .OrderBy(t => t.EndPoint.GetHashCode()).ToList();

            var recordIndex = (records.Count - 1) % serverRecordMod;
            var manager = new CallbackManager(_client);
            var steamUnifiedMessages = _client.GetHandler<SteamUnifiedMessages>()!;
            _serverRecord = records[recordIndex];
            _steamApps = _client.GetHandler<SteamApps>()!;
            _inventoryService = steamUnifiedMessages.CreateService<IInventory>();
            _playerService = steamUnifiedMessages.CreateService<IPlayer>();
            _accountTracker = accountTracker;
            _mainConfig = mainConfig;
            _loginHandler = new SteamLoginHandler(accountTracker, _client, manager, _serverRecord);
            _tokenSource = new CancellationTokenSource();
            _skipGames = new List<uint>();
            Task.Run(() =>
            {
                while (_work)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(0.2));
                }
            });
        }


        public void Start()
        {
            _task = EasyIdling(_tokenSource.Token);
        }

        public async Task StopAsync()
        {
            if (_task != null)
            {
                await _tokenSource.CancelAsync();
                await _task;
            }

            await LogOf();
        }

        private async Task EasyIdling(CancellationToken token)
        {
            try
            {
                Log.Logger.Information("Try to login");
                var res = await _loginHandler.Login(_serverRecord);
                _steamWebHandler = new SteamWebHandler(_client, _loginHandler.WebApiNonce!);

                if (res == EResult.OK)
                {
                    Log.Logger.Information("Try add license apps");
                    await AddFreeLicenseApp(_accountTracker.Account.RunConfig.AppsToAdd);
                    foreach (var packageId in _accountTracker.Account.RunConfig.PackagesToAdd)
                    {
                        await AddFreeLicensePackage(packageId);
                    }

                    await _accountTracker.ResetLicensesToAdd();

                    var playTask = PlayTask(token);
                    var dropTask = DropTask(token, _mainConfig.DropConfig);

                    await playTask;
                    await dropTask;

                    StopGame();
                }
            }
            catch (TaskCanceledException e)
            {
                Log.Logger.Error(e, "Error while EasyIdling as timout");
                Log.Logger.Information("Exit without waiting");
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "Error while EasyIdling");
                await _accountTracker.ExitWithError(
                    $"{e.Message} {e.StackTrace} {e.InnerException?.Message} {e.InnerException?.StackTrace}");
            }
        }

        /* public async Task<List<uint>?> GetOwnedGames()
         {
             return null;
             var request = new CPlayer_GetOwnedGames_Request()
             {
                 steamid = _client.SteamID!,
                 include_appinfo = false,
                 include_free_sub = true,
                 include_played_free_games = true,
                 skip_unvetted_apps = false,
             };
             try
             {
                 var response = await _playerService.SendMessage(x => x.GetOwnedGames(request)).ToLongRunningTask();

                 var body = response.GetDeserializedResponse<CPlayer_GetOwnedGames_Response>();
                 var appIds = body.games.Select(t => (uint)t.appid).ToList();
                 await _accountTracker.UpdateOwnedApps(appIds);
                 Log.Logger.Information("Got CPlayer_GetOwnedGames_Response");
                 return appIds;
             }
             catch (Exception e)
             {
                 Log.Logger.Error(e, "Error while getting CPlayer_GetOwnedGames_Response");
                 return null;
             }
         }*/

        private async Task LogOf()
        {
            _work = false;
            _client.Disconnect();
        }


        public async Task AddFreeLicenseApp(List<uint> gamesIds)
        {
            try
            {
                if (!gamesIds.Any())
                {
                    return;
                }

                await _steamApps.RequestFreeLicense(gamesIds).ToLongRunningTask();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while AddFreeLicenseApp");
            }
        }

        public async Task AddFreeLicensePackage(uint gamesId)
        {
            try
            {
                if (_steamWebHandler != null)
                {
                    await _steamWebHandler.TryAddFreeLicensePackage(gamesId);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while AddFreeLicensePackage");
            }
        }


        private async Task PlayTask(CancellationToken token)
        {
            var appIds = _mainConfig.DropConfig.Select(t => t.GameId).Distinct().ToList();
            Log.Logger.Information("Possible games [{games}]", appIds);
            var queueList = new Queue<uint>(appIds);
            while (!token.IsCancellationRequested)
            {
                await PlayGames(queueList.ToList());
                var lastItem = queueList.Dequeue();
                queueList.Enqueue(lastItem);

                await Task.Delay(1000 * 60 * 2, token);
            }
        }

        private async Task DropTask(CancellationToken token, List<DropConfig> configs)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var config in configs)
                {
                    if (_skipGames.Contains(config.GameId))
                    {
                        continue;
                    }
                    foreach (var itemId in config.DropItemIds)
                    {
                        if (_skipGames.Contains(config.GameId))
                        {
                            break;
                        }
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        try
                        {
                            await TryDropItem(token, config.GameId, itemId);
                            await Task.Delay(5_000, token);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error while DropTask");
                        }
                    }
                }
            }
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
                    if (response.Result == EResult.Fail && !string.IsNullOrEmpty(response.ErrorMessage) && response.ErrorMessage == "User must be allowed to play game to access inventory.")
                    {
                        Log.Logger.Information("response result {resp} {result}", response.Result, response.ErrorMessage);
                        await Task.Delay(5_000, token);
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
                    Log.Logger.Information("try again after wait 1 min");
                    await Task.Delay(61_000, token);
                    
                }
            } while (!executed);
        }

        /* private async Task CheckTimeItemsList(List<DropConfig> configs, List<uint> possibleGames)
         {
             Console.WriteLine("TryDrop: " + DateTime.Now.ToShortTimeString());

             foreach (var config in configs)
             {
                 if (!possibleGames.Contains(config.GameId))
                 {
                     continue;
                 }

                 foreach (var itemId in config.DropItemIds)
                 {
                     var reqkf = new CInventory_ConsumePlaytime_Request
                     {
                         appid = config.GameId,
                         itemdefid = itemId
                     };
                     Log.Logger.Information("Sent CInventory_ConsumePlaytime_Request: gameId: {gameId} itemId: {itemId}", config.GameId, itemId);
                     try
                     {
                         var response = await _inventoryService.SendMessage(x => x.ConsumePlaytime(reqkf))
                             .ToLongRunningTask();
                         var result = response.GetDeserializedResponse<CInventory_Response>();
                         if (result.item_json != "[]")
                         {
                             Log.Logger.Information("ItemDropped: {result}", result.item_json);
                             await _accountTracker.ItemDropped(result.item_json);
                         }
                     }
                     catch (Exception e)
                     {
                         Log.Logger.Error(e, "Error while sending CInventory_ConsumePlaytime_Request");
                     }
                 }
             }
         }*/

        private void StopGame()
        {
            var games = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            games.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed()
            {
                game_id = 0
            });
            _client.Send(games);
        }

        private async Task PlayGames(List<uint> gamesIds)
        {
            await _accountTracker.Plays();
            var games = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            foreach (var gameId in gamesIds)
            {
                if (_skipGames.Contains(gameId))
                {
                    continue;
                }
                games.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                {
                    game_id = new GameID(gameId),
                });
            }

            _client.Send(games);
            Log.Logger.Information("Sent CMsgClientGamesPlayed");
        }
    }
}
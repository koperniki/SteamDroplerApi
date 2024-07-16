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


        public SteamMachine(AccountTracker accountTracker, int serverRecordMod, MainConfig mainConfig)
        {
            _client = new SteamClient();
            var records = (SteamDirectory.LoadAsync(_client.Configuration).Result).ToList();
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

            Task.Run(() =>
            {
                while (_work)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
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

                    var appIds = _mainConfig.DropConfig.Select(t => t.GameId).Distinct().ToList();
                    while (!token.IsCancellationRequested)
                    {
                        Log.Logger.Information("Try to get owned apps");
                        var ownedGames = await GetOwnedGames();
                        var possibleGames = ownedGames == null ? appIds : ownedGames.Intersect(appIds).ToList();
                        Log.Logger.Information("Possible games [{games}]", possibleGames);
                        if (possibleGames.Any())
                        {
                            await PlayGames(possibleGames);
                            await CheckTimeItemsList(_mainConfig.DropConfig, possibleGames);
                        }

                        await Task.Delay(1000 * 60 * 30, token);
                    }

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

        public async Task<List<uint>?> GetOwnedGames()
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
        }

        private async Task LogOf()
        {
            _work = false;
            _client.Disconnect();
        }


        public async Task AddFreeLicenseApp(List<uint> gamesIds)
        {
            if (!gamesIds.Any())
            {
                return;
            }

            await _steamApps.RequestFreeLicense(gamesIds).ToLongRunningTask();
        }

        public async Task AddFreeLicensePackage(uint gamesId)
        {
            if (_steamWebHandler != null)
            {
                await _steamWebHandler.TryAddFreeLicensePackage(gamesId);
            }
        }

        private async Task CheckTimeItemsList(List<DropConfig> configs, List<uint> possibleGames)
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
                    Log.Logger.Information("Sent CInventory_ConsumePlaytime_Request: itemId: {itemId}", itemId);
                    try
                    {
                        var response = await _inventoryService.SendMessage(x => x.ConsumePlaytime(reqkf)).ToLongRunningTask();
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
        }

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

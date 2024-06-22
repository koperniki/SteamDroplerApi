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

        //private readonly Account _steamAccount;
        private readonly MainConfig _mainConfig;
        private readonly SteamLoginHandler _loginHandler;
        private readonly SteamClient _client;
        //private readonly SteamApps _steamApps;

        private readonly SteamUnifiedMessages.UnifiedService<IInventory> _inventoryService;
        //private readonly SteamUnifiedMessages.UnifiedService<IDeviceAuth> _deviceService;

        private bool _work = true;
        private readonly CancellationTokenSource _tokenSource;
        private Task? _task;

        public SteamMachine(AccountTracker accountTracker, int serverRecordMod, MainConfig mainConfig)
        {
            _client = new SteamClient();
            var records = (SteamDirectory.LoadAsync(_client.Configuration).Result).ToList();
            var recordIndex = (records.Count - 1) % serverRecordMod;
            _serverRecord = records[recordIndex];
            var manager = new CallbackManager(_client);
            var steamUnifiedMessages = _client.GetHandler<SteamUnifiedMessages>()!;
            _inventoryService = steamUnifiedMessages.CreateService<IInventory>();
            //_deviceService = steamUnifiedMessages.CreateService<IDeviceAuth>();
            _accountTracker = accountTracker;
          
            //_steamAccount = accountTracker.Account;
            _mainConfig = mainConfig;
            _loginHandler = new SteamLoginHandler(accountTracker, _client, manager, _serverRecord);
            //_steamApps = _client.GetHandler<SteamApps>()!;
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
                await _task;
            }

            await LogOf();
        }

        private async Task EasyIdling(CancellationToken token)
        {
            try
            {
                Console.WriteLine("tryLogin");
                var res = await _loginHandler.Login(_serverRecord);

                if (res == EResult.OK)
                {
                    var appIds = _mainConfig.DropConfig.Select(t => t.GameId).Distinct().ToList();
                    while (!token.IsCancellationRequested)
                    {
                        await PlayGames(appIds);
                        await CheckTimeItemsList(_mainConfig.DropConfig);
                        await Task.Delay(1000 * 60 * 30, token);
                    }

                    await CheckTimeItemsList(_mainConfig.DropConfig);
                    StopGame();
                }
            }
            catch (Exception e)
            {
                await _accountTracker.ExitWithError(e.Message);
            }
        }


        /*public async Task<T> Execute<T>(Func<SteamMachine, T> func)
        {

            var res = await _loginHandler.Login(SteamServerList.GetServerRecord());

            if (res == EResult.OK)
            {
                var ret = func(this);
                LogOf();
                return ret;
            }
            LogOf();
            return default(T);
        }*/

        private async Task LogOf()
        {
            await Task.Delay(5000);
            _work = false;
            _client.Disconnect();
        }


        /*private async Task AddFreeLicense(List<uint> gamesIds)
        {
            var result = await _steamApps.RequestFreeLicense(gamesIds);
            Console.WriteLine($"GrantedApps: {string.Join(",", result.GrantedApps)}");
        }*/


        private async Task CheckTimeItemsList(List<DropConfig> configs)
        {
            Console.WriteLine("TryDrop: " + DateTime.Now.ToShortTimeString());

            foreach (var config in configs)
            {
                foreach (var itemId in config.DropItemIds)
                {
                    var reqkf = new CInventory_ConsumePlaytime_Request
                    {
                        appid = config.GameId,
                        itemdefid = itemId
                    };
                    var response = await _inventoryService.SendMessage(x => x.ConsumePlaytime(reqkf));
                    var result = response.GetDeserializedResponse<CInventory_Response>();
                    if (result.item_json != "[]")
                    {
                        await _accountTracker.ItemDropped(result.item_json);
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
        }
    }
}
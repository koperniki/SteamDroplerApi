using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamKit2;
using SteamKit2.Discovery;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic
{
    public class SteamMachine
    {
        public LicenseHandler LicenseHandler { get; private set; }
        private readonly AccountTracker _accountTracker;
        private readonly ServerRecord _serverRecord;
        private readonly SteamLoginHandler _loginHandler;
        private readonly SteamClient _client;


        //private readonly SteamUnifiedMessages.UnifiedService<IPlayer> _playerService;
        private bool _work = true;
        private Task? _task;
        private readonly SteamWebHandler _steamWebHandler;
        private readonly PlayHandler _playHandler;
        private readonly DropHandler _dropHandler;


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

            _serverRecord = records[recordIndex];

            _steamWebHandler = new SteamWebHandler(_client);
            LicenseHandler = new LicenseHandler(_client, _steamWebHandler, accountTracker);
            _accountTracker = accountTracker;
            _loginHandler = new SteamLoginHandler(accountTracker, _client, manager, _serverRecord);

            var skipGames = new HashSet<uint>();
            _playHandler = new PlayHandler(accountTracker, _client, mainConfig, skipGames);
            _dropHandler = new DropHandler(accountTracker, _client, mainConfig, skipGames);

            Task.Run(() =>
            {
                while (_work)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(0.2));
                }
            });
        }


        public void Start(CancellationToken token)
        {
            _task = EasyIdling(token);
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
                Log.Logger.Information("Try to login");
                var res = await _loginHandler.Login(_serverRecord);
                

                if (res == EResult.OK)
                {
                    _steamWebHandler.SetNonce(_loginHandler.WebApiNonce!);
                    
                    await LicenseHandler.InitCheck();

                    await _dropHandler.DetectingDroppableGames(token);


                    var playTask = _playHandler.PlayTask(token);
                    var dropTask = _dropHandler.DropTask(token);

                    await playTask;
                    await dropTask;

                    _playHandler.StopGame();
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

        private Task LogOf()
        {
            _work = false;
            _client.Disconnect();
            return Task.CompletedTask;
        }
    }
}
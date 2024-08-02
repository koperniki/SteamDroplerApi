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
        private readonly SteamLoginHandler _loginHandler;
        private readonly SteamClient _client;
        private readonly SteamApps _steamApps;

        //private readonly SteamUnifiedMessages.UnifiedService<IPlayer> _playerService;
        private bool _work = true;
        private Task? _task;
        private SteamWebHandler? _steamWebHandler;
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
            _steamApps = _client.GetHandler<SteamApps>()!;

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
    }
}
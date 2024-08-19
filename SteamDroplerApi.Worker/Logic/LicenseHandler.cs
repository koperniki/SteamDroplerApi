using Serilog;
using SteamKit2;

namespace SteamDroplerApi.Worker.Logic;

public class LicenseHandler
{
    private readonly SteamClient _client;
    private readonly SteamWebHandler _steamWebHandler;
    private readonly AccountTracker _accountTracker;
    private readonly SteamApps _steamApps;

    public LicenseHandler(SteamClient client, SteamWebHandler steamWebHandler, AccountTracker accountTracker)
    {
        _client = client;
        _steamWebHandler = steamWebHandler;
        _accountTracker = accountTracker;
        _steamApps = _client.GetHandler<SteamApps>()!;
    }

    public async Task InitCheck()
    {
        Log.Logger.Information("Try add license apps");
        await AddFreeLicenseApp(_accountTracker.Account.RunConfig.AppsToAdd);
        foreach (var packageId in _accountTracker.Account.RunConfig.PackagesToAdd)
        {
            await AddFreeLicensePackage(packageId);
        }

        await _accountTracker.ResetLicensesToAdd();
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
            await _steamWebHandler.TryAddFreeLicensePackage(gamesId);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while AddFreeLicensePackage");
        }
    }
}
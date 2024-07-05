using System.Globalization;
using System.Net;
using System.Text;
using RestSharp;
using SteamKit2;

namespace SteamDroplerApi.Worker.Logic;

public class SteamWebHandler
{
    private static Uri SteamCheckoutUrl => new("https://checkout.steampowered.com");
    private static Uri SteamCommunityUrl => new("https://steamcommunity.com");
    private static Uri SteamHelpUrl => new("https://help.steampowered.com");
    private static Uri SteamStoreUrl => new("https://store.steampowered.com");
    private readonly SteamClient _steamClient;
    private readonly string _webApiUserNonce;
    private CookieContainer _cookieContainer;

    public SteamWebHandler(SteamClient steamClient, string webApiUserNonce)
    {
        _steamClient = steamClient;
        _webApiUserNonce = webApiUserNonce;
        _cookieContainer = new CookieContainer();
    }


    public async Task<bool> TryAddFreeLicensePackage(uint packageId)
    {
        await RefreshSessionIfExpired();

        using var client = new RestClient(new RestClientOptions()
        {
            CookieContainer = _cookieContainer
        });
        var request = new RestRequest(new Uri(SteamStoreUrl, $"/freelicense/addfreelicense/{packageId}"));
        var response = await client.ExecutePostAsync(request);
        return response.IsSuccessful;

    }

    private async Task RefreshSessionIfExpired()
    {
        using var client = new RestClient(new RestClientOptions()
        {
            CookieContainer = _cookieContainer
        });
        var request = new RestRequest(new Uri(SteamStoreUrl, "/account"));
        var response = await client.ExecuteAsync(request);
        var responsePath = response.ResponseUri!;
        if (responsePath.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
            responsePath.Host.Equals("lostauth", StringComparison.OrdinalIgnoreCase))
        {
            await Init();
        }
    }

    private async Task Init()
    {
        var publicKey = KeyDictionary.GetPublicKey(EUniverse.Public);
        var sessionKey = CryptoHelper.GenerateRandomBlock(32);

        using RSACrypto rsa = new(publicKey!);
        var encryptedSessionKey = rsa.Encrypt(sessionKey);

        var loginKey = Encoding.UTF8.GetBytes(_webApiUserNonce);

        var encryptedLoginKey = CryptoHelper.SymmetricEncrypt(loginKey, sessionKey);

        Dictionary<string, object?> arguments = new(3, StringComparer.Ordinal)
        {
            { "encrypted_loginkey", encryptedLoginKey },
            { "sessionkey", encryptedSessionKey },
            { "steamid", (ulong)_steamClient.SteamID! }
        };

        using var authService = _steamClient.Configuration.GetAsyncWebAPIInterface("ISteamUserAuth");

        var response = await authService.CallAsync(HttpMethod.Post, "AuthenticateUser", args: arguments);

        var steamLogin = response["token"].AsString();
        var steamLoginSecure = response["tokensecure"].AsString();

        string sessionId =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(((ulong)_steamClient.SteamID!).ToString(CultureInfo.InvariantCulture)));
        _cookieContainer = new CookieContainer();
        _cookieContainer.Add(new Cookie("sessionid", sessionId, "/", $".{SteamCheckoutUrl.Host}"));
        _cookieContainer.Add(new Cookie("sessionid", sessionId, "/", $".{SteamCommunityUrl.Host}"));
        _cookieContainer.Add(new Cookie("sessionid", sessionId, "/", $".{SteamHelpUrl.Host}"));
        _cookieContainer.Add(new Cookie("sessionid", sessionId, "/", $".{SteamStoreUrl.Host}"));

        _cookieContainer.Add(new Cookie("steamLogin", steamLogin, "/", $".{SteamCheckoutUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLogin", steamLogin, "/", $".{SteamCommunityUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLogin", steamLogin, "/", $".{SteamHelpUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLogin", steamLogin, "/", $".{SteamStoreUrl.Host}"));

        _cookieContainer.Add(new Cookie("steamLoginSecure", steamLoginSecure, "/", $".{SteamCheckoutUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLoginSecure", steamLoginSecure, "/", $".{SteamCommunityUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLoginSecure", steamLoginSecure, "/", $".{SteamHelpUrl.Host}"));
        _cookieContainer.Add(new Cookie("steamLoginSecure", steamLoginSecure, "/", $".{SteamStoreUrl.Host}"));

        var timeZoneOffset = $"{(int)DateTimeOffset.Now.Offset.TotalSeconds}{Uri.EscapeDataString(",")}0";

        _cookieContainer.Add(new Cookie("timezoneOffset", timeZoneOffset, "/", $".{SteamCheckoutUrl.Host}"));
        _cookieContainer.Add(new Cookie("timezoneOffset", timeZoneOffset, "/", $".{SteamCommunityUrl.Host}"));
        _cookieContainer.Add(new Cookie("timezoneOffset", timeZoneOffset, "/", $".{SteamHelpUrl.Host}"));
        _cookieContainer.Add(new Cookie("timezoneOffset", timeZoneOffset, "/", $".{SteamStoreUrl.Host}"));
    }
}
﻿using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;
using SteamDroplerApi.Worker.Logic.Auth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Discovery;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic
{
    public class SteamLoginHandler
    {
        private readonly AccountTracker _accountTracker;
        private readonly SteamClient _client;
        private readonly SteamUser _sUser;
        private int _tryLoginCount;
        private readonly Account _steamAccount;
        private readonly TaskCompletionSource<EResult> _loginTcs;
        private ServerRecord _serverRecord;
        private string? _refreshToken;
        public SteamLoginHandler(AccountTracker accountTracker, SteamClient client, CallbackManager manager,
            ServerRecord serverRecord)
        {
            _steamAccount = accountTracker.Account;
            _accountTracker = accountTracker;
            _client = client;
            _serverRecord = serverRecord;
            _sUser = _client.GetHandler<SteamUser>()!;

            _loginTcs = new TaskCompletionSource<EResult>();

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        }


        public Task<EResult> Login(ServerRecord serverRecord)
        {
            _serverRecord = serverRecord;
            _client.Connect(_serverRecord);
            return _loginTcs.Task;
        }


        async void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.Write("Connected to Steam! Logging in '{0}'...", _steamAccount.Name);
            if (_steamAccount.RunConfig.Token != null)
            {
                _refreshToken = _steamAccount.RunConfig.Token;
                _sUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = _steamAccount.Name,
                    AccessToken = _steamAccount.RunConfig.Token,
                    ShouldRememberPassword = true
                });
            }
            else
            {
                IAuthenticator auth;
                switch (_steamAccount.Config.AuthType)
                {
                    case AuthType.Console:
                        auth = new UserConsoleAuthenticator();
                        break;

                    case AuthType.WithSecretKey:
                        auth = new TwoFactorAuth(_steamAccount.Config.SharedSecret!);
                        break;

                    case AuthType.Device:
                    default:
                        auth = new DeviceAuth();
                        break;
                }

                try
                {
                    var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(
                        new AuthSessionDetails
                        {
                            Username = _steamAccount.Name,
                            Password = _steamAccount.Config.Password,
                            ClientOSType = EOSType.Windows10,
                            DeviceFriendlyName = _steamAccount.Name + "pc",
                            PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_SteamClient,
                            IsPersistentSession = true,
                            WebsiteID = "Client",
                            Authenticator = auth,
                        });


                    var pollResponse = await authSession.PollingWaitForResultAsync();
                    _refreshToken = pollResponse.RefreshToken;
                    _sUser.LogOn(new SteamUser.LogOnDetails
                    {
                        Username = pollResponse.AccountName,
                        AccessToken = pollResponse.RefreshToken,
                        ShouldRememberPassword = true
                    });
                }
                catch (Exception e)
                {
                    await _accountTracker.LoginWithError(e.Message);
                    Console.WriteLine(e.Message);
                    _loginTcs?.TrySetResult(EResult.UnexpectedError);
                }
            }
        }


        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected from Steam");
        }

        async void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            Console.WriteLine(callback.Result);

            _tryLoginCount++;
            if (_tryLoginCount > 5)
            {
                _loginTcs?.TrySetResult(callback.Result);
            }

            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.Expired)
                {
                    await _accountTracker.TokenExpired();
                }

                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);
                return;
            }

            await _accountTracker.LoggedIn(_client.SteamID!, _refreshToken!);
            _loginTcs?.TrySetResult(callback.Result);
        }

        void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }
    }
}
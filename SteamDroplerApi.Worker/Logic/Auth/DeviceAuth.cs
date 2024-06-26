﻿using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic.Auth;

public class DeviceAuth: IAuthenticator
{
    
    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        return Task.FromResult("");
    }

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
    {
        return Task.FromResult("");
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        return Task.FromResult( true );
    }

    public EAuthSessionGuardType NeedGuardType()
    {
        return EAuthSessionGuardType.k_EAuthSessionGuardType_DeviceConfirmation;
    }
}

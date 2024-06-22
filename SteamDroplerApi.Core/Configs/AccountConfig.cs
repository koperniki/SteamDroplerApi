namespace SteamDroplerApi.Core.Configs;

public class AccountConfig
{
    public string Password { get; set; } = null!;
    
    public bool IdleEnable { get; set; }

    public string? SharedSecret { get; set; }
    
    public AuthType AuthType { get; set; } = AuthType.WithSecretKey;
    
}



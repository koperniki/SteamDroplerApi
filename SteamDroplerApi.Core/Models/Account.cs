using SteamDroplerApi.Core.Configs;

namespace SteamDroplerApi.Core.Models;

public class Account
{
    public string Name { get;  }
    public AccountConfig Config { get; }
    public AccountRunConfig RunConfig { get; }
    public bool IsPlaying { get; set; }
    public Account(string name, AccountConfig config, AccountRunConfig runConfig)
    {
        Name = name;
        Config = config;
        RunConfig = runConfig;
    }
}
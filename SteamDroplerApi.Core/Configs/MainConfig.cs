namespace SteamDroplerApi.Core.Configs;

public class MainConfig
{

    public int StartTimeOut { get; set; } = 30;
    public int CoolDownAfterLoginError { get; set; } = 120;
    public List<DropConfig> DropConfig { get; set; } = new();
    public bool LogWorker { get; set; } = false;
    public int IdDropCooldown { get; set; } = 5;
    public int IdDropErrorCooldown { get; set; } = 60;
}
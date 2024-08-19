namespace SteamDroplerApi.Core.Models;

public class SteamDescriptions
{
    public bool RefreshApps { get; set; } = false;
    public Dictionary<uint, SteamAppDescription> Apps { get; set; } = new();
}

public class SteamAppDescription
{
    public uint AppId { get; set; }
    public bool IsFree { get; set; }
    public string Name { get; set; } = null!;
}
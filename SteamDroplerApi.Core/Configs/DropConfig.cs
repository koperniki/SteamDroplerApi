namespace SteamDroplerApi.Core.Configs;

public class DropConfig
{
    public uint GameId { get; set; }
    public List<uint> DropItemIds { get; set; } = new();
}
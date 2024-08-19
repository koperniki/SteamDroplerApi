namespace SteamDroplerApi.Core.Models;

public class InventoryResponse
{
    public bool IsValid { get; set; }
    public List<InventoryItem> Items { get; set; } = new();
    public List<ItemDescription> Descriptions { get; set; } = new();
}
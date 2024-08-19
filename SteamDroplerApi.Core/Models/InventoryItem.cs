namespace SteamDroplerApi.Core.Models;

public record InventoryItem(ulong AssetId, ulong ClassId, ulong InstanceId, ulong ContextId, long Amount);

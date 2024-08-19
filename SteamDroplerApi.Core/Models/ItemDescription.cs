using SteamKit2.Internal;

namespace SteamDroplerApi.Core.Models;

public record ItemDescription(ulong ClassId, ulong InstanceId, CEconItem_Description Description);
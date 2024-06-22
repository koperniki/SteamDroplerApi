using SteamDroplerApi.Core.Configs;
using SteamDroplerApi.Core.Models;
using SteamKit2.Discovery;

namespace SteamDroplerApi.Core;

public interface IWorkerHub
{
    Task Start(int serverRecordMod, Account account, MainConfig mainConfig);
    Task Stop();
}
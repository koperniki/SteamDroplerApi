using Serilog;
using SteamDroplerApi.Core.Configs;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamDroplerApi.Worker.Logic;

public class PlayHandler
{
    private readonly AccountTracker _accountTracker;
    private readonly SteamClient _client;
    private readonly MainConfig _mainConfig;
    private readonly HashSet<uint> _skipGames;

    public PlayHandler(AccountTracker accountTracker, SteamClient client, MainConfig mainConfig, HashSet<uint> skipGames)
    {
        _accountTracker = accountTracker;
        _client = client;
        _mainConfig = mainConfig;
        _skipGames = skipGames;
    }
    
    public async Task PlayTask(CancellationToken token)
    {
        var appIds = _mainConfig.DropConfig.Select(t => t.GameId).Distinct().ToList();
        Log.Logger.Information("Possible games [{games}]", appIds);
        var queueList = new Queue<uint>(appIds);
        while (!token.IsCancellationRequested)
        {
            await PlayGames(queueList.ToList());
            var lastItem = queueList.Dequeue();
            queueList.Enqueue(lastItem);

            await Task.Delay(1000 * 60 * 2, token);
        }
    }
    
    private async Task PlayGames(List<uint> gamesIds)
    {
        await _accountTracker.Plays();
        var games = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

        foreach (var gameId in gamesIds)
        {
            if (_skipGames.Contains(gameId))
            {
                continue;
            }

            games.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(gameId),
            });
        }

        _client.Send(games);
        Log.Logger.Information("Sent CMsgClientGamesPlayed");
    }
    
    public void StopGame()
    {
        var games = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        games.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed()
        {
            game_id = 0
        });
        _client.Send(games);
    }
}
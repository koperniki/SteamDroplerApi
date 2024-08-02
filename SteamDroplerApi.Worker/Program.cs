
using SteamDroplerApi.Worker;

var workerId = args.First();
await Client.Run(workerId, new CancellationTokenSource());

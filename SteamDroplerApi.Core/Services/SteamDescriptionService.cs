using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using SteamDroplerApi.Core.Models;

namespace SteamDroplerApi.Core.Services;

public class SteamDescriptionService
{
    public SteamDescriptions? SteamDescriptions { get; private set; }
    
    private readonly ILogger<SteamDescriptionService> _logger;
    private readonly MainConfigService _mainConfigService;
    private static readonly string DescriptionFilePath = Path.Combine("Configs", "SteamDescription.json");

    public SteamDescriptionService(ILogger<SteamDescriptionService> logger, MainConfigService mainConfigService)
    {
        _logger = logger;
        _mainConfigService = mainConfigService;
    }

    public async Task StartAsync()
    {
        if (File.Exists(DescriptionFilePath))
        {
            var data = await File.ReadAllTextAsync(DescriptionFilePath);

            try
            {
                SteamDescriptions = JsonConvert.DeserializeObject<SteamDescriptions>(data);
                _logger.LogInformation("SteamDescription.json file loaded.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Invalid SteamDescription.json file.");
                SteamDescriptions = new SteamDescriptions();
            }
        }
        else
        {
            _logger.LogError("MainConfig.json file doesnt exist. Exit app");
            SteamDescriptions = new SteamDescriptions();
        }

        await UpdateAppsDescription();
    }


    private async Task UpdateAppsDescription()
    {
        var restClient = new RestClient("https://store.steampowered.com/");
        if (SteamDescriptions!.RefreshApps)
        {
            SteamDescriptions.Apps.Clear();
        }

        var games = _mainConfigService.MainConfig!.DropConfig.Select(t => t.GameId).Distinct().ToList();

        foreach (var gameId in games)
        {
            if (!SteamDescriptions.Apps.ContainsKey(gameId))
            {
                var response = await restClient.GetAsync(new RestRequest($"api/appdetails?appids={gameId}"));
                if (response.IsSuccessful)
                {
                    var fullData = JObject.Parse(response.Content!);
                    var gameNode = fullData.GetValue(gameId.ToString());
                    if (gameNode != null && gameNode["success"]?.Value<bool>() == true)
                    {
                        var dataNode = gameNode["data"] as dynamic;
                        SteamDescriptions.Apps[gameId] = new SteamAppDescription()
                        {
                            Name = dataNode.name,
                            IsFree = dataNode.is_free,
                            AppId = gameId
                        };
                    }
                }
            }  
        }
        await Save();
    }

    private async Task Save()
    {
        await File.WriteAllTextAsync(DescriptionFilePath, JsonConvert.SerializeObject(SteamDescriptions, Formatting.Indented));
    }
}
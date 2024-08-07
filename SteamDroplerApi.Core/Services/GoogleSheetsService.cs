﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Logging;

namespace SteamDroplerApi.Core.Services;

public class GoogleSheetsService
{
    private readonly ILogger<GoogleSheetsService> _logger;
    private readonly AccountConfigService _accountConfigService;
    private readonly DropService _dropService;
    private static readonly string ConfigPath = "./GoogleConfig.json";

    public GoogleSheetsService(ILogger<GoogleSheetsService> logger, AccountConfigService accountConfigService,
        DropService dropService)
    {
        _logger = logger;
        _accountConfigService = accountConfigService;
        _dropService = dropService;
    }

    public async Task StartAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            return;
        }

        var credential = GoogleCredential.FromFile(ConfigPath).CreateScoped(SheetsService.Scope.Spreadsheets);
        var service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SteamDroplerApi",
        });
        var range = "Accounts!A1:D"; // Replace with your desired range
        var request = service.Spreadsheets.Values.Get("id", range);
        var response = await request.ExecuteAsync();
    }
}
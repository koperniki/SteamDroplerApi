using System.Diagnostics;
using Serilog;
using Serilog.Events;
using SteamDroplerApi;
using SteamDroplerApi.Core.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:7832");

Log.Logger = new LoggerConfiguration().MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
    .WriteTo.Console()
    .WriteTo.File("./logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.AddSignalR();
builder.Services.AddHostedService<StartupService>();
builder.Services.AddSingleton<MainConfigService>();
builder.Services.AddSingleton<AccountConfigService>();
builder.Services.AddSingleton<GoogleSheetsService>();
builder.Services.AddSingleton<AppSystemService>();
builder.Services.AddSingleton<WorkerService>();
builder.Services.AddSingleton<DropService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapHub<WorkerHub>("worker");

try
{
    app.Run();
}
catch
{
    if (AppSystemService.NeedRestart)
    {
        Console.WriteLine("Wait restart 5s...");
        Thread.Sleep(5000);
        var currentProcess = Path.GetFullPath(Process.GetCurrentProcess().MainModule!.FileName);
        Process.Start(currentProcess);
    }
}
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using SteamDroplerApi.Core.Configs;

namespace SteamDroplerApi.Core.Models;

public class AccountDomainWorkerProxy(Account account, MainConfig mainConfig)
{

  
    
    public string ProxyId { get; } = Guid.NewGuid().ToString();
    public string? ConnectionId { get; private set; }
    private AssemblyLoadContext? Context { get; set; }
    public Account Account => account;

    private TaskCompletionSource<bool>? _waitConnectionTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _workerTask;
    private static readonly string ExecutablePath;

    static AccountDomainWorkerProxy()
    {
        ExecutablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(ExecutablePath,
            "Microsoft.AspNetCore.SignalR.Client.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(ExecutablePath,
            "Microsoft.AspNetCore.SignalR.Client.Core.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(ExecutablePath,
            "Microsoft.AspNetCore.Http.Connections.Client.dll"));
    }
    
    public Task Do(CancellationToken token)
    {
        if (Context == null && CanBeRun())
        {
            return RunAndWaitConnection(token);
        }

        return Task.CompletedTask;
    }

    private async Task RunAndWaitConnection(CancellationToken token)
    {
        StartProcess();
        _waitConnectionTask = new TaskCompletionSource<bool>();
        _ = Task.Run(async () =>
        {
            await Task.Delay(10_000, token);
            _waitConnectionTask.TrySetResult(false);
        });

        var isConnected = await _waitConnectionTask.Task;

        if (isConnected)
        {
            await Task.Delay(TimeSpan.FromSeconds(mainConfig.StartTimeOut), token);
            return;
        }

        if (Context == null) return;
       Context.Unload();
       Context = null;
    }

    private void StartProcess()
    {
      
        _cancellationTokenSource = new CancellationTokenSource();
        Context = new AssemblyLoadContext(name: ProxyId, isCollectible: true);
        Context.LoadFromAssemblyPath(Path.Combine(ExecutablePath, "Serilog.dll"));
        var assembly = Context.LoadFromAssemblyPath(Path.Combine(ExecutablePath, "SteamDroplerApi.Worker.dll"));
        var type = assembly.GetType("SteamDroplerApi.Worker.Client")!;
        var methodInfo = type.GetMethod("Run" , BindingFlags.Static | BindingFlags.Public)!;

        _workerTask = (Task)methodInfo.Invoke(null, new object[] { ProxyId, _cancellationTokenSource })!;
        
    }

    public void WorkerConnected(string connectionId)
    {
        ConnectionId = connectionId;
        _waitConnectionTask?.TrySetResult(true);
    }


    private bool CanBeRun()
    {
        var hasNoError = account.RunConfig.LastLoginErrorTime == null ||
                         account.RunConfig.LastLoginErrorTime +
                         TimeSpan.FromSeconds(mainConfig.CoolDownAfterLoginError) <
                         DateTime.UtcNow;
        return hasNoError;
    }

    public async Task Kill()
    {
        if (_workerTask != null)
        {
            await _cancellationTokenSource!.CancelAsync();
            try
            {
                await _workerTask;
            }
            finally
            {
                Context!.Unload();
                Context = null;
            }
          
           
        }

    }

    public async Task Reload()
    {
        account.IsPlaying = false;
        ConnectionId = null;
        await Kill();
    }
}
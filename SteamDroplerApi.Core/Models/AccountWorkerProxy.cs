using System.Diagnostics;
using System.Runtime.InteropServices;
using SteamDroplerApi.Core.Configs;

namespace SteamDroplerApi.Core.Models;

public class AccountWorkerProxy(Account account, MainConfig mainConfig)
{
    public string ProxyId { get; } = Guid.NewGuid().ToString();
    public string? ConnectionId { get; private set; }
    private Process? Process { get; set; }
    public Account Account => account;

    private TaskCompletionSource<bool>? _waitConnectionTask;

    public Task Do(CancellationToken token)
    {
        if (Process == null && CanBeRun())
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

        if (Process == null) return;
        Process.Exited -= WorkerExited;
        Process.Kill();
        Process = null;
    }

    private void StartProcess()
    {
        var startInfo = new ProcessStartInfo();
        startInfo.FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? "SteamDroplerApi.Worker"
            : "SteamDroplerApi.Worker.exe";
        startInfo.Arguments = ProxyId;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        Process = new Process();
        Process.StartInfo = startInfo;
        Process.EnableRaisingEvents = true;
        Process.Exited += WorkerExited;

        Process.Start();
    }

    public void WorkerConnected(string connectionId)
    {
        ConnectionId = connectionId;
        _waitConnectionTask?.TrySetResult(true);
    }

    private void WorkerExited(object? sender, EventArgs e)
    {
    }

    private bool CanBeRun()
    {
        var hasNoError = account.RunConfig.LastLoginErrorTime == null ||
                         account.RunConfig.LastLoginErrorTime +
                         TimeSpan.FromSeconds(mainConfig.CoolDownAfterLoginError) <
                         DateTime.UtcNow;
        return hasNoError;
    }

    public Task Kill()
    {
        if (Process != null)
        {
            Process.Exited -= WorkerExited;
            Process.Kill();
            Process = null;
        }

        return Task.CompletedTask;
    }

    public async Task Reload()
    {
        account.IsPlaying = false;
        ConnectionId = null;
        await Kill();
    }
}
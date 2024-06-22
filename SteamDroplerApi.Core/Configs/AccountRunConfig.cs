﻿namespace SteamDroplerApi.Core.Configs;

public class AccountRunConfig
{
    public ulong? SteamId { get; set; }
    public string? Token { get; set; }
    public DateTime? LastLoginErrorTime { get; set; }
    public string? LoginErrorReason { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime? UpdateTime { get; set; }
}



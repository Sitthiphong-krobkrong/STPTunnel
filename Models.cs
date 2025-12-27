using System.Collections.Generic;

namespace STPTunnel;

public sealed class AppCfg
{
    // ====== Jump Host ======
    public JumpCfg Jump { get; set; } = new();

    // ====== SSH Key ======
    // เก็บ path ไว้ใน config เพื่อใช้ข้ามเครื่อง/แก้ไขได้
    public string KeyPath { get; set; } = "";

    // ====== Profiles ======
    public List<string> Profiles { get; set; } = new() { "UAT" };
    public string ActiveProfile { get; set; } = "UAT";
    public bool AutoConnect { get; set; } = false;
    // เก็บ tunnels แยกตาม profile
    public Dictionary<string, List<TunnelCfg>> ProfileTunnels { get; set; } = new();

    // ====== Helper ======
    public List<TunnelCfg> GetTunnels(string profile)
        => ProfileTunnels.TryGetValue(profile, out var list) ? list : new List<TunnelCfg>();

    public void SetTunnels(string profile, List<TunnelCfg> tunnels)
        => ProfileTunnels[profile] = tunnels;
}

public sealed class JumpCfg
{
    public string Host { get; set; } = "203.150.100.146";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "EEC-e-License";
}

public sealed class TunnelCfg
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "New Tunnel";
    public int LocalPort { get; set; } = 2200;
    public string RemoteHost { get; set; } = "192.168.42.9";
    public int RemotePort { get; set; } = 22;
}
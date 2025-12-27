using System;
using System.IO;
using System.Text.Json;

namespace STPTunnel;

public sealed class ConfigStore
{
    public string ConfigDir { get; }
    public string ConfigPath { get; }

    public ConfigStore()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var baseDir =
            OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", "STPTunnel")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STPTunnel");

        ConfigDir = baseDir;
        Directory.CreateDirectory(ConfigDir);

        ConfigPath = Path.Combine(ConfigDir, "config.json");
    }

    public bool Exists() => File.Exists(ConfigPath);

    public AppCfg CreateDefault()
    {
        var cfg = new AppCfg
        {
            Jump = new JumpCfg(),
            ActiveProfile = "UAT",
        };

        cfg.KeyPath = SshKeyHelper.DefaultKeyPath();
        cfg.Profiles = new() { "UAT" };
        cfg.ProfileTunnels["UAT"] = new();

        return cfg;
    }

    public AppCfg Load()
    {
        if (!File.Exists(ConfigPath))
            return CreateDefault();

        try
        {
            var json = File.ReadAllText(ConfigPath);

            // 1) ลองอ่านแบบ schema ใหม่ก่อน
            var cfg = JsonSerializer.Deserialize<AppCfg>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (cfg != null)
            {
                Normalize(cfg);
                return cfg;
            }

            // 2) ถ้า null ลอง migrate จาก schema เก่า
            var migrated = TryMigrateFromOldSchema(json);
            if (migrated != null)
            {
                Normalize(migrated);
                return migrated;
            }

            // 3) ถ้าไม่ได้ทั้งคู่ -> backup แล้วสร้างใหม่
            return BackupAndReset("Unknown schema");
        }
        catch (JsonException jex)
        {
            return BackupAndReset("JsonException: " + jex.Message);
        }
        catch (Exception ex)
        {
            return BackupAndReset("Exception: " + ex.Message);
        }
    }

    public void Save(AppCfg cfg)
    {
        Directory.CreateDirectory(ConfigDir);
        Normalize(cfg);

        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    // -------------------------
    // Helpers
    // -------------------------

    private AppCfg BackupAndReset(string reason)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var bak = Path.Combine(ConfigDir, $"config.bak_{stamp}.json");
            File.Copy(ConfigPath, bak, overwrite: true);
        }
        catch
        {
            // ignore backup errors
        }

        var fresh = CreateDefault();
        Save(fresh);

        // จะไม่ throw ต่อ เพื่อให้แอปเปิดได้
        return fresh;
    }

    private static void Normalize(AppCfg cfg)
    {
        cfg.Jump ??= new JumpCfg();
        if (string.IsNullOrWhiteSpace(cfg.KeyPath)) cfg.KeyPath = SshKeyHelper.DefaultKeyPath();

        cfg.Profiles ??= new() { "UAT" };
        if (cfg.Profiles.Count == 0) cfg.Profiles.Add("UAT");

        if (string.IsNullOrWhiteSpace(cfg.ActiveProfile)) cfg.ActiveProfile = cfg.Profiles[0];

        cfg.ProfileTunnels ??= new();
        if (!cfg.ProfileTunnels.ContainsKey(cfg.ActiveProfile))
            cfg.ProfileTunnels[cfg.ActiveProfile] = new();
    }

    /// <summary>
    /// รองรับ schema เก่าแบบ:
    /// JumpHost/JumpPort/JumpUser (string/int)
    /// Tunnels (List<TunnelCfg>) หรือ ProfileTunnels ที่ไม่ตรง
    /// </summary>
    private AppCfg? TryMigrateFromOldSchema(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ถ้าไม่มีคีย์แบบเก่าเลย ก็ไม่ migrate
        var hasOldJump =
            root.TryGetProperty("JumpHost", out _) ||
            root.TryGetProperty("JumpPort", out _) ||
            root.TryGetProperty("JumpUser", out _);

        var hasOldTunnels = root.TryGetProperty("Tunnels", out _);

        if (!hasOldJump && !hasOldTunnels)
            return null;

        var cfg = CreateDefault();

        // --- JumpHost/JumpPort/JumpUser -> Jump ---
        if (root.TryGetProperty("JumpHost", out var jh) && jh.ValueKind == JsonValueKind.String)
            cfg.Jump.Host = jh.GetString() ?? cfg.Jump.Host;

        if (root.TryGetProperty("JumpUser", out var ju) && ju.ValueKind == JsonValueKind.String)
            cfg.Jump.User = ju.GetString() ?? cfg.Jump.User;

        if (root.TryGetProperty("JumpPort", out var jp))
        {
            if (jp.ValueKind == JsonValueKind.Number && jp.TryGetInt32(out var p1)) cfg.Jump.Port = p1;
            if (jp.ValueKind == JsonValueKind.String && int.TryParse(jp.GetString(), out var p2)) cfg.Jump.Port = p2;
        }

        // --- KeyPath (ถ้าเก่ามี) ---
        if (root.TryGetProperty("KeyPath", out var kp) && kp.ValueKind == JsonValueKind.String)
            cfg.KeyPath = kp.GetString() ?? cfg.KeyPath;

        // --- Profiles / ActiveProfile ---
        if (root.TryGetProperty("Profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
        {
            cfg.Profiles.Clear();
            foreach (var item in profiles.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) cfg.Profiles.Add(s!);
                }
            }
            if (cfg.Profiles.Count == 0) cfg.Profiles.Add("UAT");
        }

        if (root.TryGetProperty("ActiveProfile", out var ap) && ap.ValueKind == JsonValueKind.String)
        {
            var s = ap.GetString();
            if (!string.IsNullOrWhiteSpace(s)) cfg.ActiveProfile = s!;
        }

        // --- Tunnels (เก่า) -> ใส่เข้า ActiveProfile ---
        if (root.TryGetProperty("Tunnels", out var tunnelsElem) && tunnelsElem.ValueKind == JsonValueKind.Array)
        {
            try
            {
                var list = JsonSerializer.Deserialize<System.Collections.Generic.List<TunnelCfg>>(tunnelsElem.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                cfg.ProfileTunnels[cfg.ActiveProfile] = list;
            }
            catch
            {
                cfg.ProfileTunnels[cfg.ActiveProfile] = new();
            }
        }

        return cfg;
    }
}
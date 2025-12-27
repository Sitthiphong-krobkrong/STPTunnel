using Avalonia.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace STPTunnel;

public sealed class SettingsVm : INotifyPropertyChanged
{
    private readonly ConfigStore _store;
    private AppCfg _cfg;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Bind (Jump)
    public string JumpHost { get; set; } = "";
    public string JumpPort { get; set; } = "22";
    public string JumpUser { get; set; } = "";

    public ObservableCollection<string> ProfileNames { get; } = new();

    private string _activeProfile = "UAT";
    public string ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (_activeProfile == value) return;
            _activeProfile = value;
            OnChanged();
            LoadTunnelsFromActiveProfile();
        }
    }

    public ObservableCollection<TunnelCfg> Tunnels { get; } = new();

    private TunnelCfg? _selectedTunnel;
    public TunnelCfg? SelectedTunnel
    {
        get => _selectedTunnel;
        set { _selectedTunnel = value; OnChanged(); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnChanged(); }
    }

    public SettingsVm(ConfigStore store)
    {
        _store = store;

        _cfg = _store.Exists() ? _store.Load() : _store.CreateDefault();
        if (!_store.Exists()) _store.Save(_cfg);

        // Profiles
        if (_cfg.Profiles == null || _cfg.Profiles.Count == 0)
            _cfg.Profiles = new() { "UAT" };

        ProfileNames.Clear();
        foreach (var p in _cfg.Profiles)
            ProfileNames.Add(p);

        if (string.IsNullOrWhiteSpace(_cfg.ActiveProfile) || !ProfileNames.Contains(_cfg.ActiveProfile))
            _cfg.ActiveProfile = ProfileNames.First();

        // Jump
        _cfg.Jump ??= new JumpCfg();
        JumpHost = _cfg.Jump.Host ?? "";
        JumpUser = _cfg.Jump.User ?? "";
        JumpPort = (_cfg.Jump.Port <= 0 ? 22 : _cfg.Jump.Port).ToString(CultureInfo.InvariantCulture);

        // Active profile
        ActiveProfile = _cfg.ActiveProfile;

        LoadTunnelsFromActiveProfile();
    }

    public void LoadTunnelsFromActiveProfile()
    {
        Tunnels.Clear();

        var list = _cfg.GetTunnels(ActiveProfile);
        foreach (var t in list)
            Tunnels.Add(t);

        SelectedTunnel = Tunnels.FirstOrDefault();
        StatusText = $"Loaded {ActiveProfile} ✅ (count={Tunnels.Count})";
    }

    // =========================
    // STEP B: Validate + Save
    // =========================
    public (bool ok, string message) Save()
    {
        if (!ValidateAll(out var err))
            return (false, err);

        try
        {
            // Jump
            _cfg.Jump ??= new JumpCfg();
            _cfg.Jump.Host = JumpHost.Trim();
            _cfg.Jump.User = JumpUser.Trim();
            _cfg.Jump.Port = int.Parse(JumpPort.Trim(), CultureInfo.InvariantCulture);

            // Active profile
            _cfg.ActiveProfile = ActiveProfile;

            // Tunnels
            _cfg.SetTunnels(ActiveProfile, Tunnels.ToList());

            _store.Save(_cfg);

            StatusText = "Saved ✅";
            return (true, "Saved ✅");
        }
        catch (Exception ex)
        {
            var msg = "Save failed: " + ex.Message;
            StatusText = msg;
            return (false, msg);
        }
    }

    private bool ValidateAll(out string errorText)
    {
        var sb = new StringBuilder();

        // ---- Jump validate ----
        var host = (JumpHost ?? "").Trim();
        var user = (JumpUser ?? "").Trim();
        if (string.IsNullOrWhiteSpace(host))
            sb.AppendLine("• Jump Host ห้ามว่าง");
        if (string.IsNullOrWhiteSpace(user))
            sb.AppendLine("• User ห้ามว่าง");

        if (!TryParsePort(JumpPort, out var jumpPort))
            sb.AppendLine("• Port ต้องเป็นตัวเลข 1–65535");

        // ---- Tunnels validate ----
        if (Tunnels.Count == 0)
            sb.AppendLine("• ต้องมี Tunnel อย่างน้อย 1 รายการ");

        // บังคับ: อย่างน้อย 1 ตัวเปิดใช้งาน
        if (Tunnels.Count > 0 && !Tunnels.Any(t => t.Enabled))
            sb.AppendLine("• ต้องมี Tunnel ที่เปิดใช้งาน (On) อย่างน้อย 1 รายการ");

        // LocalPort ซ้ำ (พิจารณาเฉพาะ Enabled=true)
        var dupPorts = Tunnels
            .Where(t => t.Enabled)
            .GroupBy(t => t.LocalPort)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        if (dupPorts.Count > 0)
            sb.AppendLine($"• Local Port ซ้ำ: {string.Join(", ", dupPorts)}");

        // ตรวจทีละ row
        for (int i = 0; i < Tunnels.Count; i++)
        {
            var t = Tunnels[i];
            var row = i + 1;

            if (string.IsNullOrWhiteSpace(t.Name))
                sb.AppendLine($"• Tunnel แถว {row}: Name ห้ามว่าง");

            if (t.LocalPort < 1 || t.LocalPort > 65535)
                sb.AppendLine($"• Tunnel แถว {row}: Local Port ต้องเป็น 1–65535");

            if (string.IsNullOrWhiteSpace(t.RemoteHost))
                sb.AppendLine($"• Tunnel แถว {row}: Remote Host ห้ามว่าง");

            if (t.RemotePort < 1 || t.RemotePort > 65535)
                sb.AppendLine($"• Tunnel แถว {row}: Remote Port ต้องเป็น 1–65535");
        }

        if (sb.Length > 0)
        {
            errorText = "❌ ข้อมูลยังไม่ถูกต้อง กรุณาแก้ก่อน Save\n" + sb.ToString().TrimEnd();
            return false;
        }

        // normalize ที่ผ่านแล้ว
        JumpHost = host;
        JumpUser = user;
        JumpPort = jumpPort.ToString(CultureInfo.InvariantCulture);

        // trim ชื่อ/host tunnel
        foreach (var t in Tunnels)
        {
            t.Name = (t.Name ?? "").Trim();
            t.RemoteHost = (t.RemoteHost ?? "").Trim();
        }

        errorText = "";
        return true;
    }

    private static bool TryParsePort(string? s, out int port)
    {
        port = 0;
        if (!int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            return false;
        return port >= 1 && port <= 65535;
    }

    // =========================
    // + Add / - Remove
    // =========================
    public void AddTunnel()
    {
        // หา local port ที่ยังไม่ชน
        var basePort = 2200;
        var used = Tunnels.Select(x => x.LocalPort).ToHashSet();
        var next = basePort;
        while (used.Contains(next)) next++;

        var t = new TunnelCfg
        {
            Enabled = true,
            Name = $"Tunnel-{next}",
            LocalPort = next,
            RemoteHost = "192.168.42.9",
            RemotePort = 22
        };

        Tunnels.Add(t);
        SelectedTunnel = t;
        StatusText = $"Added ✅ (count={Tunnels.Count})";
    }

    public void RemoveSelected()
    {
        if (SelectedTunnel == null)
        {
            StatusText = "Select a row first.";
            return;
        }

        var idx = Tunnels.IndexOf(SelectedTunnel);
        if (idx < 0)
        {
            StatusText = "Selected row not found.";
            return;
        }

        Tunnels.RemoveAt(idx);

        if (Tunnels.Count == 0)
            SelectedTunnel = null;
        else if (idx >= Tunnels.Count)
            SelectedTunnel = Tunnels[^1];
        else
            SelectedTunnel = Tunnels[idx];

        StatusText = $"Removed ✅ (count={Tunnels.Count})";
    }

    // =========================
    // SSH buttons (ไว้ต่อ STEP C/D)
    // =========================
    public async Task GenerateKeyAsync()
    {
        try
        {
            _cfg.KeyPath = string.IsNullOrWhiteSpace(_cfg.KeyPath)
                ? SshKeyHelper.DefaultKeyPath()
                : _cfg.KeyPath;

            await SshKeyHelper.GenerateKeyAsync(_cfg.KeyPath);
            StatusText = $"Generated key ✅ ({_cfg.KeyPath})";

            _store.Save(_cfg);
        }
        catch (Exception ex)
        {
            StatusText = "Generate key failed ❌ : " + ex.Message;
        }
    }

    public async Task CopyPublicKeyAsync(Window owner)
    {
        try
        {
            var keyPath = string.IsNullOrWhiteSpace(_cfg.KeyPath) ? SshKeyHelper.DefaultKeyPath() : _cfg.KeyPath;
            var pub = SshKeyHelper.ReadPublicKey(keyPath);

            var top = TopLevel.GetTopLevel(owner);
            if (top?.Clipboard == null)
            {
                StatusText = "Clipboard not available.";
                return;
            }

            await top.Clipboard.SetTextAsync(pub);
            StatusText = "Copied public key ✅";
        }
        catch (Exception ex)
        {
            StatusText = "Copy failed ❌ : " + ex.Message;
        }
    }

    public Task InstallKeyToJumpHostAsync(Window owner)
    {
        var host = JumpHost.Trim();
        var user = JumpUser.Trim();

        if (!int.TryParse(JumpPort, out var port))
            port = 22;

        var keyPath = SshKeyHelper.DefaultKeyPath();
        var pubPath = keyPath + ".pub";

        if (!System.IO.File.Exists(pubPath))
        {
            StatusText = "Public key not found. Generate key first.";
            return Task.CompletedTask;
        }

        var cmd = SshKeyHelper.BuildInstallAuthorizedKeysCommand(
            user,
            host,
            port,
            pubPath
        );

        StatusText = "Opening Terminal / CMD for password input (one-time)...";

        ShellLauncher.OpenShellAndRun(cmd);

        return Task.CompletedTask;
    }

    public async Task TestSshAsync()
    {
        try
        {
            var host = (JumpHost ?? "").Trim();
            var user = (JumpUser ?? "").Trim();
            if (!int.TryParse((JumpPort ?? "22").Trim(), out var port) || port <= 0) port = 22;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            {
                StatusText = "Fill Jump Host / User first.";
                return;
            }

            var keyPath = string.IsNullOrWhiteSpace(_cfg.KeyPath) ? SshKeyHelper.DefaultKeyPath() : _cfg.KeyPath;

            StatusText = "Testing SSH...";

            var (code, stdout, stderr) = await SshKeyHelper.RunSshAsync(
                host, port, user,
                identityFile: keyPath,
                remoteCommand: "echo SSH_OK");

            if (code == 0 && stdout.Contains("SSH_OK"))
                StatusText = "SSH OK ✅";
            else
                StatusText = $"SSH FAIL ❌ (code={code}) {stderr}";
        }
        catch (Exception ex)
        {
            StatusText = "SSH FAIL ❌ : " + ex.Message;
        }
    }
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
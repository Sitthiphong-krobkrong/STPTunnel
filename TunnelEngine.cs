using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace STPTunnel;

public sealed class TunnelEngine
{
    private readonly ConfigStore _store;
    private Process? _ssh;

    public TunnelEngine(ConfigStore store)
    {
        _store = store;
    }

    public async Task StartAsync(Action<string>? log = null)
    {
        await StopAsync(log); // กันซ้อน

        var cfg = _store.Load();

        var profile = string.IsNullOrWhiteSpace(cfg.ActiveProfile) ? "UAT" : cfg.ActiveProfile;
        var tunnels = cfg.GetTunnels(profile).Where(t => t.Enabled).ToList();

        if (tunnels.Count == 0)
            throw new Exception($"No enabled tunnels in profile: {profile}");

        var host = cfg.Jump?.Host?.Trim();
        var user = cfg.Jump?.User?.Trim();
        var port = cfg.Jump?.Port ?? 22;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            throw new Exception("Jump host/user is empty. Please fill in Settings and Save.");

        var keyPath = string.IsNullOrWhiteSpace(cfg.KeyPath) ? SshKeyHelper.DefaultKeyPath() : cfg.KeyPath;

        // ถ้า key ยังไม่มี ให้บอกชัด ๆ
        if (!File.Exists(keyPath))
            throw new Exception($"SSH private key not found: {keyPath} (Generate SSH Key ก่อน)");

        // สร้าง args
        // -N = no remote command
        // -T = disable pseudo-tty
        // ExitOnForwardFailure=yes = ถ้า forward พัง ให้ exit ทันที
        // ServerAlive* กันหลุดเงียบๆ
        var args = "";
        args += $"-p {port} ";
        args += $"-i \"{keyPath}\" ";
        args += "-N -T ";
        args += "-o ExitOnForwardFailure=yes ";
        args += "-o ServerAliveInterval=30 ";
        args += "-o ServerAliveCountMax=3 ";
        args += "-o StrictHostKeyChecking=accept-new ";

        foreach (var t in tunnels)
        {
            if (t.LocalPort <= 0 || t.RemotePort <= 0 || string.IsNullOrWhiteSpace(t.RemoteHost))
                throw new Exception($"Invalid tunnel config: {t.Name}");

            args += $"-L {t.LocalPort}:{t.RemoteHost}:{t.RemotePort} ";
        }

        args += $"{user}@{host}";

        log?.Invoke($"Starting SSH tunnel ({profile})...");
        log?.Invoke($"ssh {args}");

        var psi = new ProcessStartInfo
        {
            FileName = "ssh",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _ssh = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _ssh.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log?.Invoke(e.Data); };
        _ssh.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log?.Invoke(e.Data); };

        if (!_ssh.Start())
            throw new Exception("Failed to start ssh process.");

        _ssh.BeginOutputReadLine();
        _ssh.BeginErrorReadLine();

        // หน่วงนิดนึง ให้ ssh มีเวลา fail-fast ถ้า forward พัง
        await Task.Delay(600);

        if (_ssh.HasExited)
            throw new Exception("SSH exited immediately. Check key / authorized_keys / host / port.");

        log?.Invoke("Connected ✅");
    }

    public Task StopAsync(Action<string>? log = null)
    {
        try
        {
            if (_ssh == null) return Task.CompletedTask;

            if (!_ssh.HasExited)
            {
                log?.Invoke("Stopping SSH...");
                _ssh.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            log?.Invoke("Stop error: " + ex.Message);
        }
        finally
        {
            _ssh?.Dispose();
            _ssh = null;
        }

        log?.Invoke("Disconnected ❌");
        return Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync(Action<string>? log = null)
    {
        var ok = _ssh != null && !_ssh.HasExited;
        return Task.FromResult(ok);
    }

    public async Task DisconnectAsync()
    {
        if (_ssh == null)
            return;

        try
        {
            Console.WriteLine("Stopping SSH...");

            if (!_ssh.HasExited)
            {
                _ssh.Kill(entireProcessTree: true);
                await _ssh.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Disconnect error: " + ex.Message);
        }
        finally
        {
            _ssh.Dispose();
            _ssh = null;
            Console.WriteLine("Disconnected ❌");
        }
    }
}
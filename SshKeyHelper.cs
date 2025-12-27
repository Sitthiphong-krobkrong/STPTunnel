using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace STPTunnel;

public static class SshKeyHelper
{
    public static string DefaultKeyPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sshDir = Path.Combine(home, ".ssh");
        Directory.CreateDirectory(sshDir);
        return Path.Combine(sshDir, "stp_tunnel_ed25519");
    }

    public static string PublicKeyPath(string keyPath) => keyPath + ".pub";

    public static bool KeyExists(string keyPath) =>
        File.Exists(keyPath) && File.Exists(PublicKeyPath(keyPath));

    public static async Task GenerateKeyAsync(string keyPath)
    {
        var dir = Path.GetDirectoryName(keyPath)!;
        Directory.CreateDirectory(dir);

        if (KeyExists(keyPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "ssh-keygen",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("ed25519");
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(keyPath);
        psi.ArgumentList.Add("-N"); psi.ArgumentList.Add("");
        psi.ArgumentList.Add("-C"); psi.ArgumentList.Add("STPTunnel");

        var p = Process.Start(psi) ?? throw new Exception("Failed to start ssh-keygen");
        await p.WaitForExitAsync();

        if (!KeyExists(keyPath))
            throw new Exception("Key generation failed (files not found).");
    }

    public static string ReadPublicKey(string keyPath)
    {
        var pub = PublicKeyPath(keyPath);
        if (!File.Exists(pub)) throw new FileNotFoundException("Public key not found", pub);
        return File.ReadAllText(pub).Trim();
    }

    // remoteCommand = คำสั่งที่ให้ไปรันบน jump host
    public static async Task<(int exitCode, string stdout, string stderr)> RunSshAsync(
        string host, int port, string user,
        string? identityFile,
        string remoteCommand)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ssh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(port.ToString());
        psi.ArgumentList.Add("-o"); psi.ArgumentList.Add("StrictHostKeyChecking=accept-new");

        if (!string.IsNullOrWhiteSpace(identityFile))
        {
            psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(identityFile);
        }

        psi.ArgumentList.Add($"{user}@{host}");
        psi.ArgumentList.Add(remoteCommand);

        var p = Process.Start(psi) ?? throw new Exception("Failed to start ssh");
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static string BuildInstallAuthorizedKeysCommand(
     string user,
     string host,
     int port,
     string publicKeyPath)
    {
        var pub = publicKeyPath.Replace("\\", "/");

        return
            $@"ssh -p {port} {user}@{host} ""\
            mkdir -p ~/.ssh && \
            chmod 700 ~/.ssh && \
            cat {pub} >> ~/.ssh/authorized_keys && \
            chmod 600 ~/.ssh/authorized_keys && \
            echo 'SSH key installed successfully ✅'\
            """;
    }
}
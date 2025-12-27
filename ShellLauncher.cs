using System;
using System.Diagnostics;

namespace STPTunnel;

public static class ShellLauncher
{
    public static void OpenShellAndRun(string command)
    {
        if (OperatingSystem.IsMacOS())
        {
            OpenMacTerminal(command);
        }
        else if (OperatingSystem.IsWindows())
        {
            OpenWindowsCmd(command);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    // ================= macOS =================
    private static void OpenMacTerminal(string command)
    {
        // escape ให้ปลอดภัย
        var escaped = command
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        var osaScript =
            $"tell application \"Terminal\"\n" +
            $"  activate\n" +
            $"  do script \"{escaped}\"\n" +
            $"end tell";

        Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            Arguments = $"-e \"{osaScript}\"",
            UseShellExecute = true   // ⬅️ สำคัญมาก
        });
    }

    // ================= Windows =================
    private static void OpenWindowsCmd(string command)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k {command}",
            UseShellExecute = true   // ⬅️ สำคัญมาก
        });
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace STPTunnel;

public partial class App : Application
{
    
    private TrayIcon? _tray;
    private ConfigStore? _store;
    private TunnelEngine? _engine;

    private Window? _settingsWindow;

    private MenuItem _connectMenuItem = null!;
    private MenuItem _disconnectMenuItem = null!;
    private MenuItem _statusMenuItem = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ✅ สำคัญมากบน macOS: ต้องมี MainWindow
            desktop.MainWindow = new Window
            {
                Title = "STP Tunnel",
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                CanResize = false,
                Opacity = 0
            };

            desktop.MainWindow.Show();
            desktop.MainWindow.Hide();

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _store = new ConfigStore();
            if (!_store.Exists())
                _store.Save(_store.CreateDefault());

            _engine = new TunnelEngine(_store);

            SetupTray(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _tray = new TrayIcon
        {
            ToolTipText = "STP Tunnel",
            Icon = LoadIcon()
        };

        var menu = new NativeMenu();

        // --- TEST ---
        // var miPing = new NativeMenuItem("PING (test)");
        // miPing.Click += (_, __) => Log("PING clicked ✅");
        // menu.Items.Add(miPing);
        // menu.Items.Add(new NativeMenuItemSeparator());

        var miConnect = new NativeMenuItem("Connect");
        miConnect.Click += async (_, __) => await SafeRun(async () =>
        {
            await _engine!.StartAsync(Log);
            await UpdateTooltipAsync();
        });

        var miDisconnect = new NativeMenuItem("Disconnect");
        miDisconnect.Click += async (_, __) => await SafeRun(async () =>
        {
            await _engine!.StopAsync(Log);
            await UpdateTooltipAsync();
        });

        var miStatus = new NativeMenuItem("Status");
        miStatus.Click += async (_, __) => await SafeRun(async () =>
        {
            var ok = await _engine!.IsConnectedAsync(Log);
            Log(ok ? "Connected ✅" : "Disconnected ❌");
            await UpdateTooltipAsync();
        });

        var miSettings = new NativeMenuItem("Settings");
        miSettings.Click += (_, __) => OpenSettingsWindow();

        var miExit = new NativeMenuItem("Exit");
        miExit.Click += async (_, __) => await SafeRun(async () =>
        {
            await _engine!.StopAsync(Log);
            desktop.Shutdown();
        });

        menu.Items.Add(miConnect);
        menu.Items.Add(miDisconnect);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(miStatus);
        menu.Items.Add(miSettings);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(miExit);

        _tray.Menu = menu;
        _tray.IsVisible = true;

        _ = UpdateTooltipAsync();
    }

    private void OpenSettingsWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Log("OpenSettingsWindow clicked ✅");

            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_store!);
            _settingsWindow.Closed += (_, __) => _settingsWindow = null;

            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }

    private async Task UpdateTooltipAsync()
    {
        if (_tray == null || _engine == null) return;
        var ok = await _engine.IsConnectedAsync();
        _tray.ToolTipText = ok
            ? "STP Tunnel : Connected ✅"
            : "STP Tunnel : Disconnected ❌";
    }

    private async Task UpdateTrayMenuAsync()
    {
        bool connected = false;

        try
        {
            connected = await _engine.IsConnectedAsync();
        }
        catch (Exception ex)
        {
            // fallback เมื่อเช็คไม่ได้
            connected = false;
            Log($"IsConnected failed: {ex.Message}");
        }

        _connectMenuItem.IsEnabled = !connected;
        _disconnectMenuItem.IsEnabled = connected;

        _statusMenuItem.Header = connected
            ? "Status: ✅ Connected"
            : "Status: ❌ Disconnected";
    }

    private static void Log(string msg)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    private static async Task SafeRun(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
        }
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://STPTunnel/Assets/STPTunnel.png");
            using var asset = AssetLoader.Open(uri);
            return new WindowIcon(asset);
        }
        catch (Exception ex)
        {
            Console.WriteLine("LoadIcon ERROR: " + ex);
            return null;
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _engine.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connect failed: " + ex.Message);
        }
        finally
        {
            await UpdateTrayMenuAsync(); // ⭐ สำคัญ
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _engine.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Disconnect failed: " + ex.Message);
        }
        finally
        {
            await UpdateTrayMenuAsync(); // ⭐ สำคัญ
        }
    }
}
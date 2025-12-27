using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;

namespace STPTunnel;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _store;
    private DataGrid? _tunnelsGrid;

    public SettingsVm Vm { get; }

    // ✅ ต้องมี ctor เปล่าเพื่อให้ runtime loader หาเจอ
    public SettingsWindow() : this(new ConfigStore()) { }

    public SettingsWindow(ConfigStore store)
    {
        _store = store;

        InitializeComponent();

        Vm = new SettingsVm(_store);
        DataContext = Vm;

        // ✅ จับ DataGrid จาก x:Name="TunnelsGrid"
        _tunnelsGrid = this.FindControl<DataGrid>("TunnelsGrid");
        if (_tunnelsGrid != null)
        {
            _tunnelsGrid.ItemsSource = Vm.Tunnels;
            _tunnelsGrid.SelectionChanged += (_, __) =>
            {
                Vm.SelectedTunnel = _tunnelsGrid.SelectedItem as TunnelCfg;
            };
        }
        else
        {
            Console.WriteLine("WARN: TunnelsGrid not found (check x:Name in XAML)");
        }
    }

    // -----------------------------
    // Grid actions
    // -----------------------------
    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("OnAdd clicked ✅");

        Vm.AddTunnel();

        // ✅ force refresh กันเคส DataGrid ไม่ repaint
        ForceRefreshGrid();

        // เลือกแถวล่าสุดให้เลย
        if (_tunnelsGrid != null && Vm.Tunnels.Count > 0)
            _tunnelsGrid.SelectedItem = Vm.Tunnels[^1];
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("OnRemove clicked ✅");

        // sync selected จาก grid -> vm เผื่อ binding หลุด
        if (_tunnelsGrid != null)
            Vm.SelectedTunnel = _tunnelsGrid.SelectedItem as TunnelCfg;

        Vm.RemoveSelected();

        ForceRefreshGrid();
    }

    private void ForceRefreshGrid()
    {
        if (_tunnelsGrid == null) return;

        var src = _tunnelsGrid.ItemsSource;
        _tunnelsGrid.ItemsSource = null;
        _tunnelsGrid.ItemsSource = src;

        _tunnelsGrid.InvalidateMeasure();
        _tunnelsGrid.InvalidateVisual();
    }

    // -----------------------------
    // Footer
    // -----------------------------
    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var (ok, msg) = Vm.Save();

        if (ok)
            await UiDialog.Success(this, "Saved", msg);
        else
            await UiDialog.Error(this, "Invalid / Failed", msg);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    // -----------------------------
    // Config folder
    // -----------------------------
    private void OnOpenConfig(object? sender, RoutedEventArgs e)
    {
        var dir = _store.ConfigDir;
        Directory.CreateDirectory(dir);

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var psi = new System.Diagnostics.ProcessStartInfo("open");
                psi.ArgumentList.Add(dir); // ✅ ไม่แตกคำ
                System.Diagnostics.Process.Start(psi);
            }
            else if (OperatingSystem.IsWindows())
            {
                var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe");
                psi.ArgumentList.Add(dir);
                System.Diagnostics.Process.Start(psi);
            }
            else
            {
                var psi = new System.Diagnostics.ProcessStartInfo("xdg-open");
                psi.ArgumentList.Add(dir);
                System.Diagnostics.Process.Start(psi);
            }

            Vm.StatusText = "Opened config folder ✅";
        }
        catch (Exception ex)
        {
            Vm.StatusText = "Open folder failed: " + ex.Message;
        }
    }

    // -----------------------------
    // SSH Buttons
    // -----------------------------
    private async void OnGenKey(object? sender, RoutedEventArgs e) => await Vm.GenerateKeyAsync();
    private async void OnCopyPubKey(object? sender, RoutedEventArgs e) => await Vm.CopyPublicKeyAsync(this);
    private async void OnInstallKey(object? sender, RoutedEventArgs e)
    {
        var ok = await UiDialog.Confirm(
            this,
            "Install Public Key",
            "ระบบจะเปิด Terminal (mac) หรือ CMD (win) เพื่อให้กรอก SSH password (ครั้งเดียว)\nหลังจากนั้นจะไม่ต้องกรอกอีก\n\nดำเนินการต่อไหม?",
            okText: "Open Terminal/CMD",
            cancelText: "Cancel"
        );
        if (!ok) return;

        try
        {
            // จุดนี้จะ “เปิด Terminal/CMD” แล้ว return ทันที
            await Vm.InstallKeyToJumpHostAsync(this);

            await UiDialog.Info(
                this,
                "Next step",
                "ตอนนี้ไปที่ Terminal/CMD ที่เปิดขึ้นมา แล้วกรอก password ให้เสร็จ\nเสร็จแล้วกลับมากดปุ่ม “Test SSH” เพื่อยืนยันว่าใช้งานได้ ✅"
            );
        }
        catch (Exception ex)
        {
            await UiDialog.Error(this, "Error", ex.Message);
        }
    }
    private async void OnTestSsh(object? sender, RoutedEventArgs e)
    {
        try
        {
            await Vm.TestSshAsync();

            if (Vm.StatusText.Contains("OK") || Vm.StatusText.Contains("✅"))
                await UiDialog.Success(this, "SSH Test", Vm.StatusText);
            else
                await UiDialog.Error(this, "SSH Test", Vm.StatusText);
        }
        catch (Exception ex)
        {
            await UiDialog.Error(this, "SSH Test Error", ex.Message);
        }
    }
}
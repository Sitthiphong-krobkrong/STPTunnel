using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace STPTunnel;

public static class UiDialog
{
    public static Task Info(Window owner, string title, string message)
        => Show(owner, title, message, "OK");

    public static Task Success(Window owner, string title, string message)
        => Show(owner, title, message, "OK");

    public static Task Error(Window owner, string title, string message)
        => Show(owner, title, message, "OK");

    public static async Task<bool> Confirm(Window owner, string title, string message, string okText = "Continue", string cancelText = "Cancel")
    {
        var win = BuildBase(owner, title, message);

        var ok = new Button { Content = okText, MinWidth = 90 };
        var cancel = new Button { Content = cancelText, MinWidth = 90 };

        var tcs = new TaskCompletionSource<bool>();

        ok.Click += (_, __) => { tcs.TrySetResult(true); win.Close(); };
        cancel.Click += (_, __) => { tcs.TrySetResult(false); win.Close(); };

        var buttons = (StackPanel)((Panel)win.Content!).Children[2];
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        await win.ShowDialog(owner);
        return await tcs.Task;
    }

    private static async Task Show(Window owner, string title, string message, string okText)
    {
        var win = BuildBase(owner, title, message);

        var ok = new Button { Content = okText, MinWidth = 90 };
        ok.Click += (_, __) => win.Close();

        var buttons = (StackPanel)((Panel)win.Content!).Children[2];
        buttons.Children.Add(ok);

        await win.ShowDialog(owner);
    }

    private static Window BuildBase(Window owner, string title, string message)
    {
        var textTitle = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var textMsg = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 460
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var root = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 6
        };

        root.Children.Add(textTitle);
        root.Children.Add(textMsg);
        root.Children.Add(buttons);

        return new Window
        {
            Title = "STPTunnel",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = root,
            Background = owner.Background // ให้ธีมกลืน ๆ
        };
    }
}
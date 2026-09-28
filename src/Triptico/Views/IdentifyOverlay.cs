using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Triptico.Views;

/// <summary>Mostra um número grande em cada ecrã ligado, para saber qual é qual.</summary>
public static class IdentifyOverlay
{
    private static readonly List<Window> Open = [];

    public static void ShowAll()
    {
        CloseAll();
        var app = App.Current;
        app.RefreshMonitors();

        foreach (var m in app.Monitors.Where(m => m.Active))
        {
            var window = Create(app.MonitorNumber(m).ToString(), app.MonitorName(m), $"{m.Connector} · {m.Width} × {m.Height}");
            window.SourceInitialized += (_, _) =>
            {
                // Pôr a janela dentro do ecrã certo (píxeis reais) e depois maximizar lá.
                var hwnd = new WindowInteropHelper(window).Handle;
                SetWindowPos(hwnd, IntPtr.Zero, m.X + m.Width / 2 - 50, m.Y + m.Height / 2 - 50, 100, 100, SWP_NOZORDER | SWP_NOACTIVATE);
            };
            window.Loaded += (_, _) => window.WindowState = WindowState.Maximized;
            window.MouseDown += (_, _) => CloseAll();
            window.KeyDown += (_, _) => CloseAll();
            Open.Add(window);
            window.Show();
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CloseAll();
        };
        timer.Start();
    }

    private static void CloseAll()
    {
        foreach (var w in Open.ToList()) w.Close();
        Open.Clear();
    }

    private static Window Create(string number, string name, string details)
    {
        var accent = Application.Current.TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.AccentColorBrush;

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1C, 0x1C)),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(48, 24, 48, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = number, FontSize = 160, FontWeight = FontWeights.Bold, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = name, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = details, FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) },
                },
            },
        };

        return new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Width = 100,
            Height = 100,
            Content = new Border
            {
                BorderBrush = accent,
                BorderThickness = new Thickness(6),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
                Child = card,
            },
        };
    }

    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}

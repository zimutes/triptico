using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Triptico.Display;
using Triptico.Profiles;
using Triptico.Services;

namespace Triptico.Views;

public partial class MainWindow : Window
{
    private readonly App _app = App.Current;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        _app.StateChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        _loading = true;
        var current = _app.CurrentProfile;
        var cards = _app.Settings.Profiles.Select(p => new ProfileCard(p, p == current, _app)).ToList();
        ProfilesList.ItemsSource = cards;
        EmptyState.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MonitorsList.ItemsSource = _app.Monitors.Select(m => new MonitorRow(m, _app)).ToList();
        StartupCheck.IsChecked = StartupRegistration.IsEnabled;
        NotifyCheck.IsChecked = _app.Settings.ShowNotifications;
        _loading = false;
    }

    /// <summary>Só a opção "Sair" fecha mesmo a janela.</summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar = esconder; a app continua na bandeja. Para sair, usar "Sair" no menu da bandeja.
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            _app.OnMainHidden();
        }
        base.OnClosing(e);
    }

    private static Profile? ProfileOf(object sender) =>
        ((sender as FrameworkElement)?.Tag as ProfileCard ?? (sender as FrameworkElement)?.DataContext as ProfileCard)?.Profile;

    // ------------------------------------------------------------ perfis

    private void NewFromCurrent_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ProfileEditorWindow(_app.CaptureNewProfile(), isNew: true) { Owner = this };
        if (editor.ShowDialog() == true) _app.SaveProfile(editor.Result);
    }

    private void Suggested_Click(object sender, RoutedEventArgs e) => _app.CreateSuggestedProfiles();

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is { } p) await _app.ApplyProfileAsync(p);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is not { } p) return;
        var editor = new ProfileEditorWindow(p.Clone(), isNew: false) { Owner = this };
        if (editor.ShowDialog() == true) _app.SaveProfile(editor.Result);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Recapture_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is not { } p) return;
        var ok = MessageBox.Show(this,
            $"Substituir os ecrãs de «{p.Name}» pelo que está ligado agora (e pelas posições atuais)?",
            "Tríptico", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok == MessageBoxResult.Yes) _app.RecaptureProfile(p);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is { } p) _app.DuplicateProfile(p);
    }

    private void MoveLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is { } p) _app.MoveProfile(p, -1);
    }

    private void MoveRight_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is { } p) _app.MoveProfile(p, +1);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileOf(sender) is not { } p) return;
        var ok = MessageBox.Show(this, $"Apagar o perfil «{p.Name}»?", "Tríptico",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (ok == MessageBoxResult.Yes) _app.DeleteProfile(p);
    }

    // ------------------------------------------------------------ ecrãs

    private void Identify_Click(object sender, RoutedEventArgs e) => IdentifyOverlay.ShowAll();

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MonitorRow row) return;
        var name = PromptWindow.Ask(this, "Nome do ecrã",
            $"Dê um nome fácil a este ecrã (ex.: «Esquerda», «Portátil»).\nModelo: {row.Monitor.FriendlyName} · {row.Monitor.Connector}",
            _app.MonitorName(row.Monitor));
        if (name is not null) _app.SetAlias(row.Monitor, name);
    }

    // ------------------------------------------------------------ opções

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try { StartupRegistration.Set(StartupCheck.IsChecked == true); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Tríptico"); }
    }

    private void Notify_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading) _app.SetNotifications(NotifyCheck.IsChecked == true);
    }
}

public sealed class ProfileCard(Profile profile, bool isCurrent, App app)
{
    public Profile Profile { get; } = profile;
    public string Name => Profile.Name;
    public bool IsCurrent { get; } = isCurrent;
    public Visibility CurrentVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
    public bool CanApply => !app.IsApplying;

    public IReadOnlyList<PreviewItem> Preview { get; } = LayoutPreview.For(profile, app);
    public Visibility NoPreviewVisibility => Preview.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string Summary
    {
        get
        {
            var on = Profile.Monitors.Where(m => m.Enabled).ToList();
            var primary = on.FirstOrDefault(m => m.Primary) ?? on.FirstOrDefault();
            var count = on.Count == 1 ? "1 ecrã" : $"{on.Count} ecrãs";
            return primary is null ? "Nenhum ecrã" : $"{count} · principal: {app.MonitorName(primary)}";
        }
    }

    public string HotkeyText => Profile.Hotkey is null
        ? "Sem atalho"
        : app.HotkeyConflicts.Contains(Profile.Id)
            ? $"⚠ {Profile.Hotkey} já está a ser usado"
            : $"Atalho: {Profile.Hotkey}";

    public Brush? HotkeyBrush => app.HotkeyConflicts.Contains(Profile.Id)
        ? app.TryFindResource("SystemFillColorCautionBrush") as Brush ?? Brushes.DarkOrange
        : app.TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;
}

public sealed class MonitorRow(MonitorInfo monitor, App app)
{
    public MonitorInfo Monitor { get; } = monitor;
    public int Number => app.MonitorNumber(Monitor);
    public bool Active => Monitor.Active;
    public string Name => app.MonitorName(Monitor);

    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (Name != Monitor.FriendlyName) parts.Add(Monitor.FriendlyName);
            parts.Add(Monitor.Connector);
            if (Monitor.Active)
            {
                parts.Add($"{Monitor.Width} × {Monitor.Height}");
                if (Monitor.RefreshHz > 0) parts.Add($"{Monitor.RefreshHz:0} Hz");
            }
            return string.Join(" · ", parts);
        }
    }

    public string State => !Monitor.Active ? "Desligado" : Monitor.Primary ? "Principal" : "Ligado";
}

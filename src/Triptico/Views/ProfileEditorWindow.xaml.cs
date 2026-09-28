using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Triptico.Display;
using Triptico.Profiles;

namespace Triptico.Views;

public partial class ProfileEditorWindow : Window
{
    private readonly App _app = App.Current;
    private readonly Profile _profile;
    private List<EditorRow> _rows = [];
    private Hotkey? _hotkey;
    private bool _syncing;

    public Profile Result => _profile;

    public ProfileEditorWindow(Profile profile, bool isNew)
    {
        InitializeComponent();
        _profile = profile;
        _hotkey = profile.Hotkey;
        Title = isNew ? "Novo perfil" : $"Editar «{profile.Name}»";
        NameBox.Text = profile.Name;
        BuildRows(profile.Monitors);
        ShowHotkey();
        Closed += (_, _) => _app.RegisterHotkeys();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    // ------------------------------------------------------------ ecrãs

    private void BuildRows(IEnumerable<ProfileMonitor> monitors)
    {
        var list = monitors.Select(m => m.Clone()).ToList();
        var map = DisplayManager.Resolve(list, _app.Monitors);
        var byLive = map.ToDictionary(kv => kv.Value, kv => kv.Key);

        var rows = new List<EditorRow>();
        foreach (var live in _app.Monitors)
        {
            var pm = byLive.GetValueOrDefault(live) ?? new ProfileMonitor { Id = live.Id, Edid = live.Edid, Name = live.FriendlyName };
            var name = _app.MonitorName(live);
            var details = new List<string>();
            if (name != live.FriendlyName) details.Add(live.FriendlyName);
            details.Add(live.Connector);
            details.Add(live.Active ? $"{live.Width} × {live.Height}" : "desligado agora");
            rows.Add(new EditorRow(pm, $"{_app.MonitorNumber(live)}. {name}", string.Join(" · ", details)));
        }
        foreach (var pm in list.Where(p => !map.ContainsKey(p)))
            rows.Add(new EditorRow(pm, _app.MonitorName(pm), "Não está ligado ao PC agora"));

        foreach (var row in rows) row.PropertyChanged += Row_Changed;
        _rows = rows;
        Rows.ItemsSource = rows;
        EnsurePrimary();
        UpdatePreview();
    }

    private void Row_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncing || sender is not EditorRow row) return;
        _syncing = true;
        if (e.PropertyName == nameof(EditorRow.Primary) && row.Primary)
            foreach (var other in _rows.Where(r => r != row)) other.Primary = false;
        if (e.PropertyName == nameof(EditorRow.Enabled) && !row.Enabled)
            row.Primary = false;
        _syncing = false;
        EnsurePrimary();
        UpdatePreview();
    }

    private void EnsurePrimary()
    {
        if (_rows.Any(r => r.Enabled && r.Primary)) return;
        _syncing = true;
        foreach (var r in _rows) r.Primary = false;
        var first = _rows.FirstOrDefault(r => r.Enabled);
        if (first is not null) first.Primary = true;
        _syncing = false;
    }

    private void UpdatePreview()
    {
        var temp = new Profile { Monitors = _rows.Select(r => r.ToModel()).ToList() };
        Preview.Items = LayoutPreview.For(temp, _app);
    }

    private void UseCurrent_Click(object sender, RoutedEventArgs e)
    {
        var captured = _app.CaptureNewProfile(_profile.Name).Monitors;
        var keep = _rows.Select(r => r.ToModel()).Where(m => DisplayManager.Resolve([m], _app.Monitors).Count == 0);
        BuildRows(captured.Concat(keep));
    }

    // ------------------------------------------------------------ atalho

    private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Sem isto, carregar num atalho já existente aplicava esse perfil em vez de o gravar aqui.
        _app.SuspendHotkeys();
        HotkeyBox.Text = _hotkey is null ? "Carregue nas teclas…" : _hotkey.ToString();
    }

    private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _app.RegisterHotkeys();
        ShowHotkey();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            var partial = new Hotkey { Modifiers = mods, Key = Key.None }.ToString();
            HotkeyBox.Text = partial.Replace(" + None", " + …");
            return;
        }
        if (mods == ModifierKeys.None && key is Key.Escape or Key.Tab)
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }
        if (mods == ModifierKeys.None && key is Key.Back or Key.Delete)
        {
            _hotkey = null;
            HotkeyBox.Text = "Carregue nas teclas…";
            ShowHint();
            return;
        }
        if (mods == ModifierKeys.None && key is not (>= Key.F13 and <= Key.F24))
        {
            HotkeyHint.Text = "Use pelo menos uma tecla de controlo: Ctrl, Alt, Shift ou Win.";
            return;
        }

        _hotkey = new Hotkey { Modifiers = mods, Key = key };
        HotkeyBox.Text = _hotkey.ToString();
        ShowHint();
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        _hotkey = null;
        ShowHotkey();
    }

    private void ShowHotkey()
    {
        HotkeyBox.Text = _hotkey?.ToString() ?? "Clique aqui e carregue nas teclas (ex.: Ctrl + Alt + F1)";
        ShowHint();
    }

    private void ShowHint()
    {
        if (_hotkey is null)
        {
            HotkeyHint.Text = "Opcional. Funciona em qualquer lado, mesmo com a janela fechada.";
            return;
        }

        var other = _app.Settings.Profiles.FirstOrDefault(p => p.Id != _profile.Id && _hotkey.SameAs(p.Hotkey));
        var isCharKey = _hotkey.Key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or (>= Key.Oem1 and <= Key.OemBackslash);
        var ctrlAlt = _hotkey.Modifiers.HasFlag(ModifierKeys.Control) && _hotkey.Modifiers.HasFlag(ModifierKeys.Alt)
                      && !_hotkey.Modifiers.HasFlag(ModifierKeys.Shift) && !_hotkey.Modifiers.HasFlag(ModifierKeys.Windows);

        if (other is not null)
            HotkeyHint.Text = $"Este atalho é do perfil «{other.Name}»; ao guardar passa para este.";
        else if (ctrlAlt && isCharKey)
            HotkeyHint.Text = "⚠ Ctrl + Alt é o mesmo que AltGr: este atalho pode impedir de escrever @, €, { … " +
                              "Prefira Ctrl + Alt + F1…F12 ou junte o Shift.";
        else
            HotkeyHint.Text = "Funciona em qualquer lado, mesmo com a janela fechada.";
    }

    // ------------------------------------------------------------ guardar

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Dê um nome ao perfil.", "Tríptico");
            NameBox.Focus();
            return;
        }
        if (_app.Settings.Profiles.Any(p => p.Id != _profile.Id && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(this, $"Já existe um perfil chamado «{name}».", "Tríptico");
            NameBox.Focus();
            return;
        }
        if (!_rows.Any(r => r.Enabled))
        {
            MessageBox.Show(this, "Escolha pelo menos um ecrã para ficar ligado.", "Tríptico");
            return;
        }

        _profile.Name = name;
        _profile.Hotkey = _hotkey;
        _profile.Monitors = _rows.Select(r => r.ToModel()).ToList();

        // Um atalho só pode pertencer a um perfil.
        if (_hotkey is not null)
            foreach (var p in _app.Settings.Profiles.Where(p => p.Id != _profile.Id && _hotkey.SameAs(p.Hotkey)))
                p.Hotkey = null;

        DialogResult = true;
    }
}

public sealed class EditorRow(ProfileMonitor model, string title, string details) : INotifyPropertyChanged
{
    private bool _enabled = model.Enabled;
    private bool _primary = model.Enabled && model.Primary;

    public string Title { get; } = title;
    public string Details { get; } = details;

    public bool Enabled
    {
        get => _enabled;
        set { if (_enabled != value) { _enabled = value; Changed(); } }
    }

    public bool Primary
    {
        get => _primary;
        set { if (_primary != value) { _primary = value; Changed(); } }
    }

    public ProfileMonitor ToModel()
    {
        var m = model.Clone();
        m.Enabled = Enabled;
        m.Primary = Enabled && Primary;
        return m;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

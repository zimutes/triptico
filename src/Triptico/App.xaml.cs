using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Triptico.Display;
using Triptico.Profiles;
using Triptico.Services;
using Triptico.Views;

namespace Triptico;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public AppSettings Settings { get; private set; } = new();
    public List<MonitorInfo> Monitors { get; private set; } = [];
    /// <summary>Perfis cujo atalho não foi possível registar (já usado por outra app).</summary>
    public HashSet<string> HotkeyConflicts { get; } = [];
    public bool IsApplying { get; private set; }

    /// <summary>Perfis, ecrãs ou definições mudaram: as janelas devem redesenhar-se.</summary>
    public event Action? StateChanged;

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private TrayIcon? _tray;
    private HotkeyManager? _hotkeys;
    private MainWindow? _main;
    private DispatcherTimer? _displayDebounce;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Write("Erro: " + ex.Exception);
            MessageBox.Show(ex.Exception.Message, "Tríptico", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        if (CommandLine.TryRun(e.Args, out var exitCode))
        {
            Shutdown(exitCode);
            return;
        }

        var instance = @"Local\Triptico-zimutek" + SettingsStore.InstanceSuffix;
        _singleInstance = new Mutex(true, instance, out var isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, instance + "-mostrar");
        if (!isFirst)
        {
            // Já está a correr: pede à outra instância para mostrar a janela.
            _showSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowMain), null, -1, false);

        Settings = SettingsStore.Load();
        StartupRegistration.RefreshPath();
        RefreshMonitors(save: true);

        _hotkeys = new HotkeyManager();
        RegisterHotkeys();
        _tray = new TrayIcon(this);

        _displayDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _displayDebounce.Tick += (_, _) =>
        {
            _displayDebounce.Stop();
            RefreshMonitors(save: true);
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (!e.Args.Contains("--bandeja", StringComparer.OrdinalIgnoreCase))
            ShowMain();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        _displayDebounce?.Stop();
        _displayDebounce?.Start();
    });

    // ------------------------------------------------------------ janelas

    public void ShowMain()
    {
        _main ??= new MainWindow();
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    /// <summary>Chamado quando se fecha a janela principal: fica na bandeja.</summary>
    public void OnMainHidden()
    {
        if (Settings.TrayHintShown) return;
        Settings.TrayHintShown = true;
        Save();
        _tray?.Notify("O Tríptico continua aqui", "Está na bandeja, ao pé do relógio. Os atalhos continuam a funcionar.");
    }

    public void Quit()
    {
        if (_main is not null)
        {
            _main.AllowClose = true;
            _main.Close();
        }
        Shutdown();
    }

    // ------------------------------------------------------------ estado

    public void RefreshMonitors(bool save = false)
    {
        try
        {
            Monitors = DisplayManager.GetMonitors();
        }
        catch (Exception ex)
        {
            Log.Write("Falha a ler os ecrãs: " + ex.Message);
            return;
        }

        // As posições conhecidas vivem num referencial único. As do Windows são relativas ao
        // ecrã principal, que muda de perfil para perfil; por isso a disposição atual é
        // alinhada com a última posição conhecida de um ecrã que esteja ligado agora.
        var active = Monitors.Where(m => m.Active).ToList();
        var anchor = active.FirstOrDefault(m => Settings.LastKnown.ContainsKey(m.Id));
        var (dx, dy) = anchor is null ? (0, 0) : (Settings.LastKnown[anchor.Id].X - anchor.X, Settings.LastKnown[anchor.Id].Y - anchor.Y);

        var changed = false;
        foreach (var m in active)
        {
            var (x, y) = (m.X + dx, m.Y + dy);
            if (Settings.LastKnown.TryGetValue(m.Id, out var k) && k.X == x && k.Y == y && k.Width == m.Width && k.Height == m.Height)
                continue;
            Settings.LastKnown[m.Id] = new KnownRect { X = x, Y = y, Width = m.Width, Height = m.Height };
            changed = true;
        }
        if (changed && save) Save();

        // Numerar pela posição física (da esquerda para a direita), esteja o ecrã ligado ou não;
        // os que nunca estiveram ligados vão para o fim.
        Monitors = Monitors
            .OrderBy(m => Settings.LastKnown.ContainsKey(m.Id) ? 0 : 1)
            .ThenBy(m => Settings.LastKnown.TryGetValue(m.Id, out var k) ? k.X : 0)
            .ThenBy(m => Settings.LastKnown.TryGetValue(m.Id, out var k) ? k.Y : 0)
            .ThenBy(m => m.Connector)
            .ToList();

        StateChanged?.Invoke();
    }

    public Profile? CurrentProfile => Settings.Profiles.FirstOrDefault(p => DisplayManager.IsCurrent(p, Monitors));

    public string MonitorName(MonitorInfo m) =>
        Settings.Aliases.TryGetValue(m.Id, out var alias) && !string.IsNullOrWhiteSpace(alias) ? alias : m.FriendlyName;

    public string MonitorName(ProfileMonitor pm) =>
        Settings.Aliases.TryGetValue(pm.Id, out var alias) && !string.IsNullOrWhiteSpace(alias) ? alias : pm.Name;

    /// <summary>Número do ecrã (1, 2, 3...) na lista "Ecrãs ligados".</summary>
    public int MonitorNumber(MonitorInfo m) => Monitors.IndexOf(m) + 1;

    public void Save()
    {
        try { SettingsStore.Save(Settings); }
        catch (Exception ex) { Log.Write("Falha a guardar: " + ex.Message); }
    }

    public void SetAlias(MonitorInfo m, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || alias.Trim() == m.FriendlyName) Settings.Aliases.Remove(m.Id);
        else Settings.Aliases[m.Id] = alias.Trim();
        Save();
        StateChanged?.Invoke();
    }

    public void SetNotifications(bool on)
    {
        Settings.ShowNotifications = on;
        Save();
    }

    // ------------------------------------------------------------ perfis

    public void SaveProfile(Profile profile)
    {
        var i = Settings.Profiles.FindIndex(p => p.Id == profile.Id);
        if (i >= 0) Settings.Profiles[i] = profile;
        else Settings.Profiles.Add(profile);
        ProfilesChanged();
    }

    public void DeleteProfile(Profile profile)
    {
        Settings.Profiles.RemoveAll(p => p.Id == profile.Id);
        ProfilesChanged();
    }

    public void MoveProfile(Profile profile, int delta)
    {
        var i = Settings.Profiles.FindIndex(p => p.Id == profile.Id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Settings.Profiles.Count) return;
        (Settings.Profiles[i], Settings.Profiles[j]) = (Settings.Profiles[j], Settings.Profiles[i]);
        ProfilesChanged();
    }

    public void DuplicateProfile(Profile profile)
    {
        var copy = profile.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = UniqueName(profile.Name + " (cópia)");
        copy.Hotkey = null;
        Settings.Profiles.Insert(Settings.Profiles.FindIndex(p => p.Id == profile.Id) + 1, copy);
        ProfilesChanged();
    }

    /// <summary>Substitui os ecrãs do perfil pelo que está no ecrã agora (mantém nome e atalho).</summary>
    public void RecaptureProfile(Profile profile)
    {
        RefreshMonitors();
        profile.Monitors = DisplayManager.Capture(profile.Name, Monitors).Monitors;
        ProfilesChanged();
    }

    public Profile CaptureNewProfile(string? name = null)
    {
        RefreshMonitors();
        return DisplayManager.Capture(name ?? UniqueName("Perfil " + (Settings.Profiles.Count + 1)), Monitors);
    }

    /// <summary>Perfis de arranque: "Todos os ecrãs" e um "Só ..." por cada ecrã.</summary>
    public void CreateSuggestedProfiles()
    {
        RefreshMonitors();
        var all = DisplayManager.Capture(UniqueName("Todos os ecrãs"), Monitors);
        foreach (var pm in all.Monitors) pm.Enabled = true;
        all.Hotkey = FreeHotkey(Key.F1);
        Settings.Profiles.Add(all);

        var fKey = Key.F2;
        foreach (var m in Monitors)
        {
            var only = DisplayManager.Capture(UniqueName("Só " + MonitorName(m)), Monitors);
            foreach (var pm in only.Monitors)
            {
                pm.Enabled = pm.Id == m.Id;
                pm.Primary = pm.Enabled;
            }
            if (fKey <= Key.F12) only.Hotkey = FreeHotkey(fKey++);
            Settings.Profiles.Add(only);
        }
        ProfilesChanged();
    }

    private Hotkey? FreeHotkey(Key key)
    {
        var hk = new Hotkey { Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = key };
        return Settings.Profiles.Any(p => hk.SameAs(p.Hotkey)) ? null : hk;
    }

    public string UniqueName(string name)
    {
        var candidate = name;
        for (var n = 2; Settings.Profiles.Any(p => string.Equals(p.Name, candidate, StringComparison.CurrentCultureIgnoreCase)); n++)
            candidate = $"{name} {n}";
        return candidate;
    }

    private void ProfilesChanged()
    {
        Save();
        RegisterHotkeys();
        StateChanged?.Invoke();
    }

    // ------------------------------------------------------------ atalhos

    public void RegisterHotkeys()
    {
        if (_hotkeys is null) return;
        _hotkeys.UnregisterAll();
        HotkeyConflicts.Clear();
        foreach (var p in Settings.Profiles.Where(p => p.Hotkey is not null))
        {
            var id = p.Id;
            if (!_hotkeys.Register(p.Hotkey!, () => ApplyById(id)))
                HotkeyConflicts.Add(p.Id);
        }
    }

    /// <summary>Enquanto se grava um atalho novo, os globais não podem apanhar as teclas.</summary>
    public void SuspendHotkeys() => _hotkeys?.UnregisterAll();

    private void ApplyById(string id)
    {
        var p = Settings.Profiles.FirstOrDefault(x => x.Id == id);
        if (p is not null) _ = ApplyProfileAsync(p);
    }

    // ------------------------------------------------------------ ligar e desligar ecrãs

    public bool AnyMonitorOff => Monitors.Any(m => !m.Active);

    /// <summary>Liga ou desliga um ecrã, mantendo os outros como estão (não precisa de perfis).</summary>
    public Task SetMonitorAsync(MonitorInfo monitor, bool on)
    {
        RefreshMonitors();
        var profile = DisplayManager.Capture("", Monitors);
        var target = profile.Monitors.FirstOrDefault(m => m.Id == monitor.Id);
        if (target is null || target.Enabled == on) return Task.CompletedTask;
        if (!on && profile.Monitors.Count(m => m.Enabled) <= 1)
        {
            _tray?.Notify("Não dá para desligar", "É o único ecrã ligado.", error: true);
            return Task.CompletedTask;
        }

        target.Enabled = on;
        if (!on && target.Primary)
        {
            target.Primary = false;
            profile.Monitors.First(m => m.Enabled).Primary = true;
        }
        profile.Name = $"{MonitorName(monitor)} {(on ? "ligado" : "desligado")}";
        return ApplyProfileAsync(profile, profile.Name);
    }

    /// <summary>Liga todos os ecrãs ligados ao PC; o principal fica o mesmo.</summary>
    public Task EnableAllAsync()
    {
        RefreshMonitors();
        var profile = DisplayManager.Capture("Todos os ecrãs ligados", Monitors);
        foreach (var m in profile.Monitors) m.Enabled = true;
        return ApplyProfileAsync(profile, profile.Name);
    }

    // ------------------------------------------------------------ aplicar

    /// <param name="doneTitle">Título do aviso quando não é um perfil guardado (ex.: «ASUS ligado»).</param>
    public async Task ApplyProfileAsync(Profile profile, string? doneTitle = null)
    {
        if (IsApplying) return;
        IsApplying = true;
        StateChanged?.Invoke();
        var okTitle = doneTitle ?? $"«{profile.Name}» aplicado";
        var failTitle = doneTitle is null ? $"Não deu para aplicar «{profile.Name}»" : "Não deu para mudar os ecrãs";
        try
        {
            var snapshot = profile.Clone();
            var known = Settings.KnownPositionsFor(profile);
            var result = await Task.Run(() => DisplayManager.Apply(snapshot, id => known.TryGetValue(id, out var r) ? r : null));
            Log.Write($"Perfil «{profile.Name}»: {result.Message}");

            RefreshMonitors();
            if (!result.Success)
            {
                _tray?.Notify(failTitle, result.Message, error: true);
                return;
            }

            // Guardar as posições que faltavam, para a próxima vez ficar igual.
            var map = DisplayManager.Resolve(profile.Monitors, Monitors);
            foreach (var (pm, m) in map)
            {
                if (!pm.Enabled || !m.Active || pm.HasPosition) continue;
                (pm.X, pm.Y, pm.Width, pm.Height) = (m.X, m.Y, m.Width, m.Height);
            }
            Save();

            if (result.Missing.Count > 0)
                _tray?.Notify(okTitle, "Não encontrei: " + string.Join(", ", result.Missing));
            else if (Settings.ShowNotifications)
                _tray?.Notify(okTitle, "");
        }
        catch (Exception ex)
        {
            Log.Write("Erro a aplicar: " + ex);
            _tray?.Notify(failTitle, ex.Message, error: true);
        }
        finally
        {
            IsApplying = false;
            StateChanged?.Invoke();
        }
    }
}

/// <summary>
/// Linha de comandos, para atalhos no ambiente de trabalho, Stream Deck, etc.:
///   Triptico.exe --perfil "Trabalho"   aplica e sai
///   Triptico.exe --listar              mostra ecrãs e perfis
///   Triptico.exe --testar "Trabalho"   pergunta ao Windows se aceitaria, sem mudar nada
///   Triptico.exe --ligar-todos         liga todos os ecrãs ligados ao PC
/// </summary>
internal static class CommandLine
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        var cmd = args.FirstOrDefault()?.ToLowerInvariant();
        if (cmd is not ("--perfil" or "--listar" or "--testar" or "--ligar-todos")) return false;

        AttachConsole(-1);
        Console.WriteLine();
        try
        {
            var settings = SettingsStore.Load();
            if (cmd == "--listar")
            {
                var monitors = DisplayManager.GetMonitors();
                Console.WriteLine("Ecrãs:");
                for (var i = 0; i < monitors.Count; i++)
                {
                    var m = monitors[i];
                    Console.WriteLine($"  {i + 1}. {m.FriendlyName} [{m.Connector}] " +
                        (m.Active ? $"{m.Width}x{m.Height} @ ({m.X},{m.Y}) {m.RefreshHz:0.#} Hz{(m.Primary ? " principal" : "")}" : "desligado"));
                    Console.WriteLine($"     {m.Id}");
                }
                Console.WriteLine("Perfis:");
                foreach (var p in settings.Profiles)
                    Console.WriteLine($"  - {p.Name}{(p.Hotkey is null ? "" : $" ({p.Hotkey})")}{(DisplayManager.IsCurrent(p, monitors) ? "  <- atual" : "")}");
                return true;
            }

            Profile? profile;
            if (cmd == "--ligar-todos")
            {
                profile = DisplayManager.Capture("Todos os ecrãs", DisplayManager.GetMonitors());
                foreach (var m in profile.Monitors) m.Enabled = true;
            }
            else
            {
                var name = string.Join(' ', args.Skip(1));
                profile = settings.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
            }
            if (profile is null)
            {
                Console.WriteLine($"Perfil «{string.Join(' ', args.Skip(1))}» não existe.");
                exitCode = 2;
                return true;
            }

            var known = settings.KnownPositionsFor(profile);
            var result = DisplayManager.Apply(profile, id => known.TryGetValue(id, out var r) ? r : null, validateOnly: cmd == "--testar");
            Console.WriteLine($"{profile.Name}: {result.Message}" + (result.Missing.Count > 0 ? " Em falta: " + string.Join(", ", result.Missing) : ""));
            exitCode = result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
            exitCode = 1;
        }
        return true;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}

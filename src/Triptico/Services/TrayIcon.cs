using System.Drawing;
using System.Windows;
using Triptico.Views;
using WinForms = System.Windows.Forms;

namespace Triptico.Services;

/// <summary>Ícone ao pé do relógio: clique abre a janela, botão direito mostra os perfis.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu = new();

    public TrayIcon(App app)
    {
        _app = app;
        _menu.Font = new Font("Segoe UI", 9.5f);
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            e.Cancel = false;
        };

        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Tríptico",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) _app.ShowMain();
        };
        _icon.BalloonTipClicked += (_, _) => _app.ShowMain();

        _app.StateChanged += UpdateTooltip;
        UpdateTooltip();
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();
        var current = _app.CurrentProfile;

        if (_app.Settings.Profiles.Count == 0)
            _menu.Items.Add(new WinForms.ToolStripMenuItem("Ainda não há perfis") { Enabled = false });

        foreach (var profile in _app.Settings.Profiles)
        {
            var p = profile;
            var item = new WinForms.ToolStripMenuItem(p.Name)
            {
                Checked = p == current,
                ShortcutKeyDisplayString = p.Hotkey?.ToString(),
                Enabled = !_app.IsApplying,
            };
            item.Click += (_, _) => _ = _app.ApplyProfileAsync(p);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        // Sempre presente, mesmo sem perfis: é a saída de emergência.
        var all = new WinForms.ToolStripMenuItem("Ligar todos os ecrãs") { Enabled = _app.AnyMonitorOff && !_app.IsApplying };
        all.Click += (_, _) => _ = _app.EnableAllAsync();
        _menu.Items.Add(all);
        _menu.Items.Add("Abrir o Tríptico", null, (_, _) => _app.ShowMain());
        _menu.Items.Add("Identificar ecrãs", null, (_, _) => IdentifyOverlay.ShowAll());
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add("Sair", null, (_, _) => _app.Quit());
    }

    private void UpdateTooltip()
    {
        var current = _app.CurrentProfile?.Name;
        var text = current is null ? "Tríptico" : $"Tríptico · {current}";
        _icon.Text = text.Length > 120 ? text[..120] : text;
    }

    public void Notify(string title, string text, bool error = false)
    {
        _icon.ShowBalloonTip(3000, title, string.IsNullOrEmpty(text) ? " " : text,
            error ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.None);
    }

    private static Icon LoadIcon()
    {
        var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/triptico.ico"));
        using var stream = info!.Stream;
        return new Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _app.StateChanged -= UpdateTooltip;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}

using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Triptico.Profiles;

namespace Triptico.Services;

/// <summary>Atalhos de teclado globais (funcionam com a app escondida na bandeja).</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    private readonly HwndSource _window;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 1;

    public HotkeyManager()
    {
        // Janela só de mensagens (HWND_MESSAGE), invisível.
        _window = new HwndSource(new HwndSourceParameters("TripticoHotkeys") { ParentWindow = new IntPtr(-3) });
        _window.AddHook(WndProc);
    }

    public bool Register(Hotkey hotkey, Action action)
    {
        uint mods = MOD_NOREPEAT;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= MOD_ALT;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Control)) mods |= MOD_CONTROL;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= MOD_SHIFT;
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= MOD_WIN;

        var id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, mods, (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key)))
            return false;
        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_window.Handle, id);
        _actions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

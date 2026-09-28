using Microsoft.Win32;

namespace Triptico.Services;

/// <summary>Arrancar com o Windows (chave Run do utilizador, sem precisar de administrador).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Triptico";

    private static string Command => $"\"{Environment.ProcessPath}\" --bandeja";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Se a app mudou de sítio, atualiza o caminho guardado.</summary>
    public static void RefreshPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string current && current != Command)
            key.SetValue(ValueName, Command);
    }
}

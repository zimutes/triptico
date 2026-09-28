using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Triptico.Profiles;

/// <summary>Guarda tudo em %APPDATA%\Triptico\definicoes.json.</summary>
public static class SettingsStore
{
    /// <summary>A variável TRIPTICO_DADOS permite usar outra pasta (testes, modo portátil).</summary>
    public static readonly string Folder =
        Environment.GetEnvironmentVariable("TRIPTICO_DADOS") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Triptico");

    public static readonly string FilePath = Path.Combine(Folder, "definicoes.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Write("Falha a ler as definições: " + ex.Message);
            // Guardar o ficheiro estragado de lado em vez de o perder.
            try { File.Copy(FilePath, FilePath + ".estragado", overwrite: true); } catch { }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}

public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                var path = Path.Combine(SettingsStore.Folder, "registo.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}

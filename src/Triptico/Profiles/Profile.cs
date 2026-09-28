using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Triptico.Profiles;

/// <summary>Um perfil: que ecrãs ficam ligados, qual é o principal e onde fica cada um.</summary>
public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Hotkey? Hotkey { get; set; }
    public List<ProfileMonitor> Monitors { get; set; } = [];

    public Profile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Hotkey = Hotkey is null ? null : new Hotkey { Modifiers = Hotkey.Modifiers, Key = Hotkey.Key },
        Monitors = Monitors.Select(m => m.Clone()).ToList(),
    };
}

public sealed class ProfileMonitor
{
    /// <summary>Caminho do dispositivo do monitor (estável entre arranques).</summary>
    public string Id { get; set; } = "";
    /// <summary>Fabricante + produto do EDID, para reconhecer o monitor se o caminho mudar.</summary>
    public string Edid { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Primary { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    [JsonIgnore]
    public bool HasPosition => X is not null && Y is not null;

    public ProfileMonitor Clone() => (ProfileMonitor)MemberwiseClone();
}

public sealed class Hotkey
{
    public ModifierKeys Modifiers { get; set; }
    public Key Key { get; set; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join(" + ", parts);
    }

    public bool SameAs(Hotkey? other) => other is not null && other.Modifiers == Modifiers && other.Key == Key;

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (int)(key - Key.NumPad0),
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        _ => key.ToString(),
    };
}

/// <summary>Posição de um monitor da última vez que esteve ligado.</summary>
public sealed class KnownRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class AppSettings
{
    public List<Profile> Profiles { get; set; } = [];
    /// <summary>Nomes dados pelo utilizador aos monitores (id → nome).</summary>
    public Dictionary<string, string> Aliases { get; set; } = [];
    /// <summary>Última posição conhecida de cada monitor (id → retângulo).</summary>
    public Dictionary<string, KnownRect> LastKnown { get; set; } = [];
    public bool ShowNotifications { get; set; } = true;
    public bool TrayHintShown { get; set; }
}

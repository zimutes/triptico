using System.Globalization;
using System.Windows;

namespace Triptico;

/// <summary>
/// Textos da interface, em inglês (por omissão) e em português de Portugal. O XAML usa-os como
/// {DynamicResource L.Chave}; o código, com Loc.T e Loc.F. Trocar de língua atualiza as janelas
/// abertas na hora.
/// </summary>
public static class Loc
{
    internal static readonly Dictionary<string, (string En, string Pt)> Texts = new()
    {
        // Janela principal
        ["Tagline"] = ("Your screens, your way, in one click.", "Os seus ecrãs, como quer, num clique."),
        ["Profiles"] = ("Profiles", "Perfis"),
        ["SaveCurrent"] = ("Save current layout", "Guardar disposição atual"),
        ["NoProfilesTitle"] = ("No profiles yet", "Ainda não há perfis"),
        ["NoProfilesText"] = (
            "A profile remembers which screens are on and where each one sits, so you can switch with a shortcut. " +
            "Create the suggested ones (all screens, plus one with each screen alone), or arrange your screens " +
            "in Windows the way you like and click “Save current layout”. " +
            "To turn a screen on or off right now, use the buttons in the list below.",
            "Um perfil guarda que ecrãs ficam ligados e onde fica cada um, para trocar com um atalho. " +
            "Crie os sugeridos (todos os ecrãs + um só com cada ecrã) ou arrume os ecrãs no Windows como gosta " +
            "e clique em «Guardar disposição atual». " +
            "Para ligar ou desligar um ecrã já, use os botões na lista em baixo."),
        ["CreateSuggested"] = ("Create suggested profiles", "Criar perfis sugeridos"),
        ["NoLayout"] = ("No layout saved", "Sem disposição guardada"),
        ["ActiveBadge"] = ("active", "ativo"),
        ["Apply"] = ("Apply", "Aplicar"),
        ["Edit"] = ("Edit", "Editar"),
        ["MoreOptions"] = ("More options", "Mais opções"),
        ["Recapture"] = ("Update with the current layout", "Atualizar com a disposição atual"),
        ["Duplicate"] = ("Duplicate", "Duplicar"),
        ["MoveLeft"] = ("Move left", "Mover para a esquerda"),
        ["MoveRight"] = ("Move right", "Mover para a direita"),
        ["Delete"] = ("Delete", "Apagar"),
        ["ScreensTitle"] = ("Screens connected to this PC", "Ecrãs ligados ao PC"),
        ["TurnAllOn"] = ("Turn all on", "Ligar todos"),
        ["IdentifyScreens"] = ("Identify screens", "Identificar ecrãs"),
        ["TurnOn"] = ("Turn on", "Ligar"),
        ["TurnOff"] = ("Turn off", "Desligar"),
        ["TurnOffTip"] = ("Turns this screen off in Windows; the others stay as they are.",
                          "Desliga este ecrã no Windows; os outros ficam como estão."),
        ["Rename"] = ("Rename", "Mudar nome"),
        ["StartWithWindows"] = ("Start with Windows", "Iniciar com o Windows"),
        ["NotifyOnSwitch"] = ("Let me know when the profile changes", "Avisar quando troco de perfil"),
        ["Language"] = ("Language", "Língua"),
        ["HintBefore"] = ("If a screen goes blank, use “Turn all screens on” in the tray icon (or ",
                          "Se um ecrã ficar sem imagem, use «Ligar todos os ecrãs» no ícone da bandeja (ou "),
        ["HintAfter"] = (" → “Extend”). Closing this window leaves Tríptico in the tray, by the clock.",
                         " → «Expandir»). Fechar esta janela deixa o Tríptico na bandeja, ao pé do relógio."),
        ["RecaptureConfirm"] = ("Replace the screens of “{0}” with what is on now (and the current positions)?",
                                "Substituir os ecrãs de «{0}» pelo que está ligado agora (e pelas posições atuais)?"),
        ["DeleteConfirm"] = ("Delete the profile “{0}”?", "Apagar o perfil «{0}»?"),
        ["RenameTitle"] = ("Screen name", "Nome do ecrã"),
        ["RenamePrompt"] = ("Give this screen an easy name (e.g. “Left”, “Laptop”).\nModel: {0} · {1}",
                            "Dê um nome fácil a este ecrã (ex.: «Esquerda», «Portátil»).\nModelo: {0} · {1}"),
        ["OneScreen"] = ("1 screen", "1 ecrã"),
        ["NScreens"] = ("{0} screens", "{0} ecrãs"),
        ["SummaryMain"] = ("{0} · main: {1}", "{0} · principal: {1}"),
        ["NoScreen"] = ("No screen", "Nenhum ecrã"),
        ["NoHotkey"] = ("No shortcut", "Sem atalho"),
        ["HotkeyInUse"] = ("⚠ {0} is already in use", "⚠ {0} já está a ser usado"),
        ["HotkeyIs"] = ("Shortcut: {0}", "Atalho: {0}"),
        ["StateOff"] = ("Off", "Desligado"),
        ["StateMain"] = ("Main", "Principal"),
        ["StateOn"] = ("On", "Ligado"),

        // Editor de perfis
        ["ProfileTitle"] = ("Profile", "Perfil"),
        ["NewProfile"] = ("New profile", "Novo perfil"),
        ["EditProfile"] = ("Edit “{0}”", "Editar «{0}»"),
        ["Name"] = ("Name", "Nome"),
        ["Screens"] = ("Screens", "Ecrãs"),
        ["ScreensHelp"] = ("Tick the ones that stay on and choose the main one (where the taskbar and new windows go).",
                           "Marque os que ficam ligados e escolha o principal (onde ficam a barra de tarefas e as janelas novas)."),
        ["Main"] = ("Main", "Principal"),
        ["Shortcut"] = ("Keyboard shortcut", "Atalho de teclado"),
        ["Clear"] = ("Clear", "Limpar"),
        ["Cancel"] = ("Cancel", "Cancelar"),
        ["Save"] = ("Save", "Guardar"),
        ["UseCurrent"] = ("Use current layout", "Usar disposição atual"),
        ["UseCurrentTip"] = ("Copies the screens that are on now, and their positions in Windows, into this profile.",
                             "Copia para este perfil os ecrãs que estão ligados agora e as posições que têm no Windows."),
        ["OffNow"] = ("off right now", "desligado agora"),
        ["NotConnected"] = ("Not connected to this PC right now", "Não está ligado ao PC agora"),
        ["PressKeys"] = ("Press the keys…", "Carregue nas teclas…"),
        ["NeedModifier"] = ("Use at least one modifier key: Ctrl, Alt, Shift or Win.",
                            "Use pelo menos uma tecla de controlo: Ctrl, Alt, Shift ou Win."),
        ["ClickAndPress"] = ("Click here and press the keys (e.g. Ctrl + Alt + F1)",
                             "Clique aqui e carregue nas teclas (ex.: Ctrl + Alt + F1)"),
        ["HotkeyOptional"] = ("Optional. Works anywhere, even with the window closed.",
                              "Opcional. Funciona em qualquer lado, mesmo com a janela fechada."),
        ["HotkeyWorks"] = ("Works anywhere, even with the window closed.",
                           "Funciona em qualquer lado, mesmo com a janela fechada."),
        ["HotkeyTaken"] = ("This shortcut belongs to “{0}”; saving moves it here.",
                           "Este atalho é do perfil «{0}»; ao guardar passa para este."),
        ["AltGrWarning"] = ("⚠ Ctrl + Alt is the same as AltGr: this shortcut may stop you typing @, €, { … " +
                            "Prefer Ctrl + Alt + F1…F12, or add Shift.",
                            "⚠ Ctrl + Alt é o mesmo que AltGr: este atalho pode impedir de escrever @, €, { … " +
                            "Prefira Ctrl + Alt + F1…F12 ou junte o Shift."),
        ["NeedName"] = ("Give the profile a name.", "Dê um nome ao perfil."),
        ["NameExists"] = ("There is already a profile called “{0}”.", "Já existe um perfil chamado «{0}»."),
        ["NeedScreen"] = ("Choose at least one screen to stay on.", "Escolha pelo menos um ecrã para ficar ligado."),

        // Bandeja e avisos
        ["TurnAllScreensOn"] = ("Turn all screens on", "Ligar todos os ecrãs"),
        ["OpenApp"] = ("Open Tríptico", "Abrir o Tríptico"),
        ["Quit"] = ("Quit", "Sair"),
        ["StillHereTitle"] = ("Tríptico is still here", "O Tríptico continua aqui"),
        ["StillHereText"] = ("It's in the tray, by the clock. Shortcuts keep working.",
                             "Está na bandeja, ao pé do relógio. Os atalhos continuam a funcionar."),
        ["CantTurnOffTitle"] = ("Can't turn it off", "Não dá para desligar"),
        ["CantTurnOffText"] = ("It's the only screen on.", "É o único ecrã ligado."),
        ["ScreenTurnedOn"] = ("{0} on", "{0} ligado"),
        ["ScreenTurnedOff"] = ("{0} off", "{0} desligado"),
        ["AllScreensOn"] = ("All screens on", "Todos os ecrãs ligados"),
        ["ProfileApplied"] = ("“{0}” applied", "«{0}» aplicado"),
        ["CouldNotApply"] = ("Couldn't apply “{0}”", "Não deu para aplicar «{0}»"),
        ["CouldNotChange"] = ("Couldn't change the screens", "Não deu para mudar os ecrãs"),
        ["NotFound"] = ("Not found: {0}", "Não encontrei: {0}"),

        // Nomes que a app dá
        ["ProfileN"] = ("Profile {0}", "Perfil {0}"),
        ["AllScreens"] = ("All screens", "Todos os ecrãs"),
        ["OnlyScreen"] = ("Only {0}", "Só {0}"),
        ["CopySuffix"] = ("{0} (copy)", "{0} (cópia)"),
        ["GenericScreen"] = ("Generic screen", "Ecrã genérico"),
        ["ConnectorInternal"] = ("Internal", "Interno"),
        ["ConnectorOther"] = ("Other", "Outro"),

        // Resultados (avisos e linha de comandos)
        ["NoneConnected"] = ("None of this profile's screens is connected to this PC.",
                             "Nenhum dos ecrãs deste perfil está ligado ao PC."),
        ["WindowsRefused"] = ("Windows refused the change (error {0}).", "O Windows recusou a mudança de ecrãs (erro {0})."),
        ["ValidScreens"] = ("Valid (screens).", "Válido (topologia)."),
        ["Valid"] = ("Valid.", "Válido."),
        ["Applied"] = ("Applied.", "Aplicado."),
        ["CliScreens"] = ("Screens:", "Ecrãs:"),
        ["CliOff"] = ("off", "desligado"),
        ["CliMain"] = ("main", "principal"),
        ["CliProfiles"] = ("Profiles:", "Perfis:"),
        ["CliCurrent"] = ("<- current", "<- atual"),
        ["CliNoProfile"] = ("There is no profile “{0}”.", "Perfil «{0}» não existe."),
        ["CliMissing"] = (" Missing: {0}", " Em falta: {0}"),
        ["CliError"] = ("Error: {0}", "Erro: {0}"),
        ["ApiFailed"] = ("{0} failed (error {1}).", "{0} falhou (erro {1})."),
    };

    /// <summary>"en" (por omissão) ou "pt".</summary>
    public static string Language { get; private set; } = "en";

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "pt" ? "pt-PT" : "en-GB");

    public static event Action? Changed;

    public static string T(string key) =>
        Texts.TryGetValue(key, out var v) ? (Language == "pt" ? v.Pt : v.En) : key;

    public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);

    public static string Normalize(string? code) => code == "pt" ? "pt" : "en";

    /// <summary>Escolhe a língua e põe os textos nos recursos da app (o XAML atualiza-se sozinho).</summary>
    public static void Apply(string? code)
    {
        Language = Normalize(code);
        if (Application.Current is { } app)
            foreach (var key in Texts.Keys)
                app.Resources["L." + key] = T(key);
        Changed?.Invoke();
    }
}

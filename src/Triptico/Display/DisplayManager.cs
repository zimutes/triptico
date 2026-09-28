using System.Runtime.InteropServices;
using Triptico.Profiles;
using static Triptico.Display.DisplayConfigNative;

namespace Triptico.Display;

/// <summary>Um monitor ligado ao PC (ativo ou não no Windows).</summary>
public sealed class MonitorInfo
{
    public string Id { get; init; } = "";
    public string Edid { get; init; } = "";
    public string FriendlyName { get; init; } = "";
    public string Connector { get; init; } = "";
    public bool Active { get; internal set; }
    public bool Primary => Active && X == 0 && Y == 0;
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public double RefreshHz { get; internal set; }

    internal (long Adapter, uint Target) Key { get; init; }

    public LayoutRect Bounds => new(X, Y, Width, Height);
}

public sealed record ApplyResult(bool Success, string Message, IReadOnlyList<string> Missing)
{
    public static ApplyResult Fail(string message) => new(false, message, []);
}

/// <summary>Lê e muda a configuração de ecrãs do Windows (API CCD).</summary>
public static class DisplayManager
{
    private static readonly object Gate = new();

    /// <summary>Monitores ligados ao PC, os ativos primeiro (da esquerda para a direita).</summary>
    public static List<MonitorInfo> GetMonitors()
    {
        lock (Gate)
        {
            var (paths, modes) = Query(QDC_ALL_PATHS);
            return Sort(Discover(paths, modes));
        }
    }

    /// <summary>Cria um perfil a partir do que está no ecrã agora.</summary>
    public static Profile Capture(string name, IEnumerable<MonitorInfo>? monitors = null)
    {
        var list = (monitors ?? GetMonitors()).ToList();
        return new Profile
        {
            Name = name,
            Monitors = list.Select(m => new ProfileMonitor
            {
                Id = m.Id,
                Edid = m.Edid,
                Name = m.FriendlyName,
                Enabled = m.Active,
                Primary = m.Primary,
                X = m.Active ? m.X : null,
                Y = m.Active ? m.Y : null,
                Width = m.Active ? m.Width : null,
                Height = m.Active ? m.Height : null,
            }).ToList(),
        };
    }

    /// <summary>Indica se o perfil corresponde ao que está ativo agora.</summary>
    public static bool IsCurrent(Profile profile, IReadOnlyList<MonitorInfo> monitors)
    {
        var map = Resolve(profile.Monitors, monitors);
        var wanted = profile.Monitors.Where(m => m.Enabled && map.ContainsKey(m)).Select(m => map[m]).ToHashSet();
        if (wanted.Count == 0) return false;
        if (!wanted.SetEquals(monitors.Where(m => m.Active))) return false;
        var primary = profile.Monitors.FirstOrDefault(m => m.Enabled && m.Primary && map.ContainsKey(m));
        return primary is null || map[primary].Primary;
    }

    /// <summary>
    /// Associa os monitores do perfil aos monitores ligados: primeiro pelo caminho do
    /// dispositivo; se não existir (ex.: drivers reinstalados), pelo modelo (EDID).
    /// </summary>
    public static Dictionary<ProfileMonitor, MonitorInfo> Resolve(IEnumerable<ProfileMonitor> wanted, IEnumerable<MonitorInfo> monitors)
    {
        var list = monitors.ToList();
        var pending = wanted.ToList();
        var map = new Dictionary<ProfileMonitor, MonitorInfo>();
        var taken = new HashSet<MonitorInfo>();

        foreach (var pm in pending)
        {
            var hit = list.FirstOrDefault(m => !taken.Contains(m) && m.Id == pm.Id);
            if (hit is null) continue;
            map[pm] = hit;
            taken.Add(hit);
        }
        foreach (var pm in pending.Where(p => !map.ContainsKey(p) && p.Edid.Length > 0))
        {
            var hit = list.FirstOrDefault(m => !taken.Contains(m) && m.Edid == pm.Edid);
            if (hit is null) continue;
            map[pm] = hit;
            taken.Add(hit);
        }
        return map;
    }

    /// <summary>
    /// Aplica um perfil: liga/desliga os ecrãs e depois acerta posições e o principal.
    /// Com <paramref name="validateOnly"/> só pergunta ao Windows se aceitaria (não muda nada).
    /// </summary>
    public static ApplyResult Apply(Profile profile, Func<string, LayoutRect?>? lastKnown = null, bool validateOnly = false)
    {
        lock (Gate)
        {
            var (paths, modes) = Query(QDC_ALL_PATHS);
            var monitors = Discover(paths, modes);
            var map = Resolve(profile.Monitors, monitors);

            var wanted = profile.Monitors.Where(m => m.Enabled && map.ContainsKey(m)).ToList();
            var missing = profile.Monitors.Where(m => m.Enabled && !map.ContainsKey(m)).Select(m => m.Name).ToList();
            if (wanted.Count == 0)
                return ApplyResult.Fail("Nenhum dos ecrãs deste perfil está ligado ao PC.");

            var wantedKeys = wanted.Select(m => map[m].Key).ToHashSet();
            var activeKeys = monitors.Where(m => m.Active).Select(m => m.Key).ToHashSet();

            if (!wantedKeys.SetEquals(activeKeys))
            {
                var primaryPm = wanted.FirstOrDefault(m => m.Primary);
                var primaryKey = primaryPm is null ? ((long, uint)?)null : map[primaryPm].Key;
                var (error, notDriven) = ApplyTopology(paths, modes, wantedKeys, primaryKey, validateOnly);
                if (error != ERROR_SUCCESS)
                    return ApplyResult.Fail($"O Windows recusou a mudança de ecrãs (erro {error}).");
                foreach (var key in notDriven)
                    missing.Add(monitors.First(m => m.Key == key).FriendlyName);
                if (validateOnly)
                    return new ApplyResult(true, "Válido (topologia).", missing);
            }

            var layoutError = ArrangeLayout(profile, lastKnown, validateOnly);
            if (layoutError != ERROR_SUCCESS)
                Log.Write($"Não foi possível acertar a disposição (erro {layoutError}).");

            return new ApplyResult(true, validateOnly ? "Válido." : "Aplicado.", missing);
        }
    }

    // ---------------------------------------------------------------- internos

    private static (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) Query(uint flags)
    {
        for (var attempt = 0; ; attempt++)
        {
            var err = GetDisplayConfigBufferSizes(flags, out var numPaths, out var numModes);
            if (err != ERROR_SUCCESS) throw new DisplayException("GetDisplayConfigBufferSizes", err);

            var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
            err = QueryDisplayConfig(flags, ref numPaths, paths, ref numModes, modes, IntPtr.Zero);
            // A configuração pode mudar entre as duas chamadas; tentar de novo.
            if (err == ERROR_INSUFFICIENT_BUFFER && attempt < 5) continue;
            if (err != ERROR_SUCCESS) throw new DisplayException("QueryDisplayConfig", err);

            Array.Resize(ref paths, (int)numPaths);
            Array.Resize(ref modes, (int)numModes);
            return (paths, modes);
        }
    }

    private static List<MonitorInfo> Discover(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        var found = new Dictionary<(long, uint), MonitorInfo>();
        foreach (var p in paths)
        {
            var t = p.targetInfo;
            if (t.targetAvailable == 0) continue;

            var key = (t.adapterId.Value, t.id);
            if (!found.TryGetValue(key, out var info))
            {
                var name = GetTargetName(t.adapterId, t.id);
                var tech = OutputTechnologyName(name?.outputTechnology ?? t.outputTechnology);
                var friendly = string.IsNullOrWhiteSpace(name?.monitorFriendlyDeviceName) ? "Ecrã genérico" : name!.Value.monitorFriendlyDeviceName;
                var path = name?.monitorDevicePath;
                info = new MonitorInfo
                {
                    Id = string.IsNullOrEmpty(path) ? $"{t.adapterId.Value:X}:{t.id}" : path,
                    Edid = name is { } n && (n.flags & 0x4) != 0 ? $"{n.edidManufactureId:X4}-{n.edidProductCodeId:X4}" : "",
                    FriendlyName = friendly,
                    Connector = tech,
                    Key = key,
                };
                found[key] = info;
            }

            if ((p.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0 && !info.Active)
            {
                info.Active = true;
                var si = p.sourceInfo.modeInfoIdx;
                if (si < modes.Length && modes[si].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    var sm = modes[si].sourceMode;
                    info.X = sm.position.x;
                    info.Y = sm.position.y;
                    info.Width = (int)sm.width;
                    info.Height = (int)sm.height;
                }
                var ti = p.targetInfo.modeInfoIdx;
                var rate = ti < modes.Length && modes[ti].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_TARGET
                    ? modes[ti].targetMode.targetVideoSignalInfo.vSyncFreq
                    : t.refreshRate;
                info.RefreshHz = rate.Denominator == 0 ? 0 : (double)rate.Numerator / rate.Denominator;
            }
        }
        return found.Values.ToList();
    }

    private static List<MonitorInfo> Sort(List<MonitorInfo> monitors) => monitors
        .OrderByDescending(m => m.Active)
        .ThenBy(m => m.Active ? m.X : 0)
        .ThenBy(m => m.Active ? m.Y : 0)
        .ThenBy(m => m.Connector)
        .ToList();

    private static DISPLAYCONFIG_TARGET_DEVICE_NAME? GetTargetName(LUID adapter, uint targetId)
    {
        var req = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                adapterId = adapter,
                id = targetId,
            },
        };
        return DisplayConfigGetDeviceInfo(ref req) == ERROR_SUCCESS ? req : null;
    }

    private static (long, uint) TargetKey(in DISPLAYCONFIG_PATH_INFO p) => (p.targetInfo.adapterId.Value, p.targetInfo.id);
    private static (long, uint) SourceKey(in DISPLAYCONFIG_PATH_INFO p) => (p.sourceInfo.adapterId.Value, p.sourceInfo.id);

    /// <summary>Liga exatamente os ecrãs pedidos. Devolve o erro do Windows e os que não deu para ligar.</summary>
    private static (int Error, List<(long, uint)> NotDriven) ApplyTopology(
        DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes,
        HashSet<(long, uint)> wanted, (long, uint)? primary, bool validateOnly)
    {
        var chosen = new List<(DISPLAYCONFIG_PATH_INFO Path, bool WasActive)>();
        var covered = new HashSet<(long, uint)>();
        var usedSources = new HashSet<(long, uint)>();

        // 1) Ecrãs que já estão ligados e ficam: manter o caminho (e a fonte) atual.
        foreach (var p in paths)
        {
            var k = TargetKey(p);
            if ((p.flags & DISPLAYCONFIG_PATH_ACTIVE) == 0 || !wanted.Contains(k) || !covered.Add(k)) continue;
            chosen.Add((p, true));
            usedSources.Add(SourceKey(p));
        }

        // 2) Ecrãs a ligar: primeira fonte livre da placa gráfica.
        foreach (var p in paths)
        {
            var k = TargetKey(p);
            if (!wanted.Contains(k) || covered.Contains(k) || p.targetInfo.targetAvailable == 0) continue;
            if (usedSources.Contains(SourceKey(p))) continue;
            chosen.Add((p, false));
            covered.Add(k);
            usedSources.Add(SourceKey(p));
        }

        var notDriven = wanted.Where(k => !covered.Contains(k)).ToList();
        if (chosen.Count == 0) return (87 /* ERROR_INVALID_PARAMETER */, notDriven);

        var action = validateOnly ? SDC_VALIDATE : SDC_APPLY;
        var save = validateOnly ? 0 : SDC_SAVE_TO_DATABASE;

        // Só a topologia, sem modos: o Windows escolhe resolução e posições.
        var bare = chosen.Select(c => Bare(c.Path)).ToArray();

        // Tentativa A: o Windows recupera a disposição que já conhece para este conjunto de ecrãs.
        var err = SetDisplayConfig((uint)bare.Length, bare, 0, null, action | SDC_TOPOLOGY_SUPPLIED | SDC_ALLOW_PATH_ORDER_CHANGES);
        Log.Write($"Topologia (base de dados): {err}");
        if (err == ERROR_SUCCESS) return (err, notDriven);

        // Tentativa B: manter os modos dos ecrãs que ficam; o Windows completa os novos.
        var modeList = new List<DISPLAYCONFIG_MODE_INFO>();
        var remap = new Dictionary<uint, uint>();
        uint Keep(uint idx)
        {
            if (idx >= modes.Length) return DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            if (!remap.TryGetValue(idx, out var n))
            {
                n = (uint)modeList.Count;
                modeList.Add(modes[idx]);
                remap[idx] = n;
            }
            return n;
        }

        var withModes = new DISPLAYCONFIG_PATH_INFO[chosen.Count];
        for (var i = 0; i < chosen.Count; i++)
        {
            var (p, wasActive) = chosen[i];
            if (!wasActive) { withModes[i] = Bare(p); continue; }
            p.flags = DISPLAYCONFIG_PATH_ACTIVE;
            p.sourceInfo.modeInfoIdx = Keep(p.sourceInfo.modeInfoIdx);
            p.targetInfo.modeInfoIdx = Keep(p.targetInfo.modeInfoIdx);
            withModes[i] = p;
        }
        // Se o principal atual vai desligar, alguém tem de ficar em (0,0).
        NormalizeOrigin(withModes, modeList, primary);

        var modeArray = modeList.ToArray();
        err = SetDisplayConfig((uint)withModes.Length, withModes, (uint)modeArray.Length, modeArray,
            action | save | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
        Log.Write($"Topologia (modos mantidos): {err}");
        if (err == ERROR_SUCCESS) return (err, notDriven);

        // Tentativa C: tudo por conta do Windows.
        err = SetDisplayConfig((uint)bare.Length, bare, 0, null, action | save | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
        Log.Write($"Topologia (sem modos): {err}");
        return (err, notDriven);
    }

    private static DISPLAYCONFIG_PATH_INFO Bare(DISPLAYCONFIG_PATH_INFO p)
    {
        p.flags = DISPLAYCONFIG_PATH_ACTIVE;
        p.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        p.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        // Caminhos inativos vêm com valores a zero, que não são válidos para ligar.
        if (p.targetInfo.rotation == 0) p.targetInfo.rotation = 1;      // IDENTITY
        if (p.targetInfo.scaling == 0) p.targetInfo.scaling = 128;      // PREFERRED
        return p;
    }

    private static void NormalizeOrigin(DISPLAYCONFIG_PATH_INFO[] paths, List<DISPLAYCONFIG_MODE_INFO> modes, (long, uint)? primary)
    {
        var sourceIdx = paths
            .Where(p => p.sourceInfo.modeInfoIdx < modes.Count)
            .Select(p => (Idx: (int)p.sourceInfo.modeInfoIdx, Target: TargetKey(p)))
            .ToList();
        if (sourceIdx.Count == 0) return;
        if (sourceIdx.Any(s => modes[s.Idx].sourceMode.position is { x: 0, y: 0 })) return;

        var origin = sourceIdx.FirstOrDefault(s => s.Target == primary);
        if (origin == default) origin = sourceIdx[0];
        var o = modes[origin.Idx].sourceMode.position;
        foreach (var idx in sourceIdx.Select(s => s.Idx).Distinct())
        {
            var m = modes[idx];
            m.sourceMode.position.x -= o.x;
            m.sourceMode.position.y -= o.y;
            modes[idx] = m;
        }
    }

    /// <summary>Acerta posições e o ecrã principal de acordo com o perfil.</summary>
    private static int ArrangeLayout(Profile profile, Func<string, LayoutRect?>? lastKnown, bool validateOnly)
    {
        var (paths, modes) = Query(QDC_ONLY_ACTIVE_PATHS);
        var monitors = Discover(paths, modes);
        var map = Resolve(profile.Monitors, monitors);
        var byMonitor = map.ToDictionary(kv => kv.Value, kv => kv.Key);

        var entries = new List<(int ModeIdx, MonitorInfo Monitor, ProfileMonitor? Wanted)>();
        var seen = new HashSet<uint>();
        foreach (var p in paths)
        {
            var idx = p.sourceInfo.modeInfoIdx;
            if (idx >= modes.Length || !seen.Add(idx)) continue; // clones partilham a mesma fonte
            var mon = monitors.FirstOrDefault(m => m.Key == TargetKey(p));
            if (mon is null) continue;
            entries.Add(((int)idx, mon, byMonitor.GetValueOrDefault(mon)));
        }
        if (entries.Count == 0) return ERROR_SUCCESS;

        var current = entries.Select(e => e.Monitor.Bounds).ToArray();
        var primary = entries.FindIndex(e => e.Wanted is { Primary: true });
        if (primary < 0) primary = entries.FindIndex(e => e.Monitor.Primary);
        if (primary < 0) primary = 0;

        // Posição desejada: a do perfil; se faltar, a última conhecida; senão, a que o Windows escolheu.
        LayoutRect? Desired(int i)
        {
            var (_, mon, pm) = entries[i];
            if (pm is { HasPosition: true }) return new LayoutRect(pm.X!.Value, pm.Y!.Value, mon.Width, mon.Height);
            if (lastKnown?.Invoke(mon.Id) is { } lk) return lk with { Width = mon.Width, Height = mon.Height };
            return null;
        }

        var desired = Enumerable.Range(0, entries.Count).Select(Desired).ToArray();
        var target = desired.All(d => d is not null)
            ? LayoutMath.Compact(desired.Select(d => d!.Value).ToArray(), primary)
            : LayoutMath.Compact(current, primary);

        var err = SetPositions(paths, modes, entries.Select(e => e.ModeIdx).ToArray(), current, target, validateOnly);
        if (err != ERROR_SUCCESS && desired.All(d => d is not null))
        {
            // Plano B: manter a disposição do Windows e só garantir o principal.
            target = LayoutMath.Compact(current, primary);
            err = SetPositions(paths, modes, entries.Select(e => e.ModeIdx).ToArray(), current, target, validateOnly);
        }
        return err;
    }

    private static int SetPositions(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes,
        int[] modeIdx, LayoutRect[] current, LayoutRect[] target, bool validateOnly)
    {
        if (current.SequenceEqual(target)) return ERROR_SUCCESS;

        var newModes = (DISPLAYCONFIG_MODE_INFO[])modes.Clone();
        for (var i = 0; i < modeIdx.Length; i++)
        {
            newModes[modeIdx[i]].sourceMode.position.x = target[i].X;
            newModes[modeIdx[i]].sourceMode.position.y = target[i].Y;
        }

        var flags = SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES
            | (validateOnly ? SDC_VALIDATE : SDC_APPLY | SDC_SAVE_TO_DATABASE);
        var err = SetDisplayConfig((uint)paths.Length, paths, (uint)newModes.Length, newModes, flags);
        Log.Write($"Disposição: {err}");
        return err;
    }
}

public sealed class DisplayException(string api, int error)
    : Exception($"{api} falhou (erro {error}).")
{
    public int Error { get; } = error;
}

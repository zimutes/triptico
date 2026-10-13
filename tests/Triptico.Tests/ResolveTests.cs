using Triptico.Display;
using Triptico.Profiles;

namespace Triptico.Tests;

/// <summary>Reconhecer os ecrãs de um perfil noutro dia, noutro arranque ou com os cabos trocados.</summary>
public class ResolveTests
{
    private static MonitorInfo Live(string id, string edid, string name = "Ecrã", int target = 0) =>
        new() { Id = id, Edid = edid, FriendlyName = name, Key = (1, (uint)target) };

    private static ProfileMonitor Saved(string id, string edid) => new() { Id = id, Edid = edid, Enabled = true };

    [Fact]
    public void FoundByDevicePath()
    {
        var a = Live(@"\\?\DISPLAY#AAA1#1", "AAA-0001", target: 1);
        var b = Live(@"\\?\DISPLAY#BBB2#2", "BBB-0002", target: 2);
        var pa = Saved(a.Id, a.Edid);
        var pb = Saved(b.Id, b.Edid);
        var map = DisplayManager.Resolve([pa, pb], [b, a]);
        Assert.Same(a, map[pa]);
        Assert.Same(b, map[pb]);
    }

    [Fact]
    public void FoundByModelWhenThePathChanged()
    {
        // Controladores reinstalados ou outra porta: o caminho muda, o modelo não.
        var live = Live(@"\\?\DISPLAY#AAA1#NEW", "AAA-0001");
        var saved = Saved(@"\\?\DISPLAY#AAA1#OLD", "AAA-0001");
        Assert.Same(live, DisplayManager.Resolve([saved], [live])[saved]);
    }

    [Fact]
    public void TwoIdenticalScreensAreNeverTheSameOne()
    {
        // Dois monitores iguais (mesmo EDID) com os caminhos mudados: cada um fica com o seu.
        var x = Live(@"\\?\DISPLAY#SAME#NEW1", "DEL-40B6", target: 1);
        var y = Live(@"\\?\DISPLAY#SAME#NEW2", "DEL-40B6", target: 2);
        var p1 = Saved(@"\\?\DISPLAY#SAME#OLD1", "DEL-40B6");
        var p2 = Saved(@"\\?\DISPLAY#SAME#OLD2", "DEL-40B6");
        var map = DisplayManager.Resolve([p1, p2], [x, y]);
        Assert.Equal(2, map.Count);
        Assert.NotSame(map[p1], map[p2]);
    }

    [Fact]
    public void MissingScreenIsLeftOut()
    {
        // Um perfil feito noutro PC, ou com um monitor que foi para o trabalho.
        var here = Live(@"\\?\DISPLAY#AAA1#1", "AAA-0001");
        var gone = Saved(@"\\?\DISPLAY#ZZZ9#9", "ZZZ-0009");
        var map = DisplayManager.Resolve([Saved(here.Id, here.Edid), gone], [here]);
        Assert.Single(map);
        Assert.False(map.ContainsKey(gone));
    }

    [Fact]
    public void ScreenWithoutEdidIsOnlyFoundByPath()
    {
        // Alguns adaptadores (e ecrãs virtuais) não dão EDID: sem caminho igual, não se adivinha.
        var live = Live(@"\\?\DISPLAY#VIRT#2", "");
        var saved = Saved(@"\\?\DISPLAY#VIRT#1", "");
        Assert.Empty(DisplayManager.Resolve([saved], [live]));
    }
}

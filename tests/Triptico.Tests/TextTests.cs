using System.IO;
using System.Text.RegularExpressions;

namespace Triptico.Tests;

/// <summary>A interface em inglês e em português: nenhum texto em falta nem a meio.</summary>
public class TextTests
{
    private static string Source(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Triptico")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine([dir!.FullName, "src", "Triptico", .. parts]);
    }

    [Fact]
    public void EveryTextExistsInBothLanguagesWithTheSamePlaceholders()
    {
        static string[] Holes(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
        foreach (var (key, (en, pt)) in Loc.Texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(en), key);
            Assert.False(string.IsNullOrWhiteSpace(pt), key);
            Assert.Equal(Holes(en), Holes(pt));
        }
    }

    [Fact]
    public void EveryTextUsedInTheWindowsExists()
    {
        var used = Directory.EnumerateFiles(Source(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{DynamicResource L\.(\w+)\}").Select(m => m.Groups[1].Value))
            .ToHashSet();
        Assert.NotEmpty(used);
        Assert.DoesNotContain(used, k => !Loc.Texts.ContainsKey(k));
    }

    [Fact]
    public void EveryTextUsedInTheCodeExists()
    {
        var used = Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Loc\.[TF]\(""(\w+)""").Select(m => m.Groups[1].Value))
            .ToHashSet();
        Assert.NotEmpty(used);
        Assert.DoesNotContain(used, k => !Loc.Texts.ContainsKey(k));
    }

    [Fact]
    public void EnglishByDefaultPortugueseOnRequest()
    {
        Loc.Apply(null);
        Assert.Equal("Turn all screens on", Loc.T("TurnAllScreensOn"));
        Loc.Apply("pt");
        Assert.Equal("Ligar todos os ecrãs", Loc.T("TurnAllScreensOn"));
        Assert.Equal("Só ASUS", Loc.F("OnlyScreen", "ASUS"));
        Loc.Apply("xx");
        Assert.Equal("en", Loc.Language);
    }
}

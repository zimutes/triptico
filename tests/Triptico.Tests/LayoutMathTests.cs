using Triptico.Display;

namespace Triptico.Tests;

/// <summary>
/// A arrumação dos ecrãs tem de servir a qualquer montagem: um ecrã, dois, oito; deitados ou
/// ao alto; tamanhos misturados; o principal em qualquer sítio; ecrãs desligados pelo meio.
/// </summary>
public class LayoutMathTests
{
    // Tamanhos que aparecem em PCs reais: portátil, 1080p, 1440p, 4K, ultrawide, 5:4, ao alto.
    private static readonly (int W, int H)[] Sizes =
    [
        (1366, 768), (1920, 1080), (2560, 1440), (3840, 2160), (3440, 1440),
        (1280, 1024), (1080, 1920), (1440, 2560), (1920, 1200), (2880, 1800),
    ];

    [Fact]
    public void OneScreenAwayFromTheOriginGoesToTheOrigin()
    {
        // Um perfil com um só ecrã que, no perfil, estava à direita de outro (issue #1).
        var result = LayoutMath.Compact([new LayoutRect(1920, 0, 2560, 1440)], 0);
        Assert.Equal(new LayoutRect(0, 0, 2560, 1440), result[0]);
    }

    [Fact]
    public void TurningOffTheMiddleScreenClosesTheGap()
    {
        var result = LayoutMath.Compact([new LayoutRect(-1920, 0, 1920, 1080), new LayoutRect(2560, 0, 1280, 1024)], 0);
        Assert.Equal(new LayoutRect(0, 0, 1920, 1080), result[0]);
        Assert.Equal(new LayoutRect(1920, 0, 1280, 1024), result[1]);
    }

    [Fact]
    public void PrimaryOnTheRightKeepsTheOthersOnTheLeft()
    {
        var result = LayoutMath.Compact(
            [new LayoutRect(-3840, 0, 1920, 1080), new LayoutRect(-1920, 0, 1920, 1080), new LayoutRect(0, 0, 2560, 1440)], 2);
        Assert.Equal(new LayoutRect(0, 0, 2560, 1440), result[2]);
        Assert.True(result[1].Right == 0 && result[0].Right == result[1].X);
    }

    [Fact]
    public void ScreenAboveStaysAbove()
    {
        var result = LayoutMath.Compact([new LayoutRect(0, 0, 3840, 2160), new LayoutRect(960, -1080, 1920, 1080)], 0);
        Assert.Equal(-1080, result[1].Y);
        Assert.Equal(960, result[1].X);
    }

    /// <summary>
    /// Montagens ao acaso, como as do Windows (cada ecrã encostado a outro), com ecrãs desligados
    /// e o principal em qualquer lado: o resultado tem sempre o principal em (0,0), nada por cima
    /// de nada e todos os ecrãs ligados uns aos outros.
    /// </summary>
    [Fact]
    public void RandomDesksAlwaysComeOutValid()
    {
        var random = new Random(20261013);
        for (var run = 0; run < 4000; run++)
        {
            var desk = RandomDesk(random, random.Next(1, 9));
            // Desliga alguns (fica pelo menos um).
            var kept = desk.Where(_ => random.NextDouble() < 0.7).ToList();
            if (kept.Count == 0)
                kept.Add(desk[random.Next(desk.Count)]);
            var primary = random.Next(kept.Count);
            var result = LayoutMath.Compact(kept, primary);
            AssertValid(result, primary, $"run {run}: {string.Join(" ", kept)}");
        }
    }

    /// <summary>
    /// Posições que vêm de alturas diferentes (perfil + últimas conhecidas) podem chegar
    /// sobrepostas ou com buracos: tem de dar uma disposição válida na mesma.
    /// </summary>
    [Fact]
    public void MessyPositionsStillComeOutValid()
    {
        var random = new Random(7);
        for (var run = 0; run < 4000; run++)
        {
            var n = random.Next(1, 9);
            var rects = Enumerable.Range(0, n).Select(_ =>
            {
                var (w, h) = Sizes[random.Next(Sizes.Length)];
                return new LayoutRect(random.Next(-8000, 8000), random.Next(-4000, 4000), w, h);
            }).ToList();
            var primary = random.Next(n);
            var result = LayoutMath.Compact(rects, primary);
            AssertValid(result, primary, $"run {run}: {string.Join(" ", rects)}");
        }
    }

    /// <summary>Paredes de vídeo e salas de controlo: até 16 ecrãs.</summary>
    [Fact]
    public void BigWallsComeOutValid()
    {
        var random = new Random(16);
        for (var run = 0; run < 1500; run++)
        {
            var desk = RandomDesk(random, random.Next(9, 17));
            var kept = desk.Where(_ => random.NextDouble() < 0.8).ToList();
            if (kept.Count == 0)
                kept.Add(desk[0]);
            var primary = random.Next(kept.Count);
            AssertValid(LayoutMath.Compact(kept, primary), primary, $"run {run}: {string.Join(" ", kept)}");
        }
    }

    private static List<LayoutRect> RandomDesk(Random random, int count)
    {
        var placed = new List<LayoutRect>();
        var (w0, h0) = Sizes[random.Next(Sizes.Length)];
        placed.Add(new LayoutRect(0, 0, w0, h0));
        var guard = 0;
        while (placed.Count < count && guard++ < 1000)
        {
            var (w, h) = Sizes[random.Next(Sizes.Length)];
            var anchor = placed[random.Next(placed.Count)];
            LayoutRect r = random.Next(4) switch
            {
                0 => new LayoutRect(anchor.Right, anchor.Y + random.Next(-h + 1, anchor.Height), w, h),
                1 => new LayoutRect(anchor.X - w, anchor.Y + random.Next(-h + 1, anchor.Height), w, h),
                2 => new LayoutRect(anchor.X + random.Next(-w + 1, anchor.Width), anchor.Bottom, w, h),
                _ => new LayoutRect(anchor.X + random.Next(-w + 1, anchor.Width), anchor.Y - h, w, h),
            };
            if (placed.All(p => !p.Overlaps(r)))
                placed.Add(r);
        }
        return placed;
    }

    private static void AssertValid(LayoutRect[] result, int primary, string context)
    {
        Assert.True(result[primary].X == 0 && result[primary].Y == 0, $"principal fora de (0,0) — {context}");
        for (var i = 0; i < result.Length; i++)
            for (var j = i + 1; j < result.Length; j++)
                Assert.False(result[i].Overlaps(result[j]), $"{result[i]} e {result[j]} sobrepostos — {context}");
        if (result.Length > 1)
            Assert.True(Connected(result), $"ecrãs soltos: {string.Join(" ", result)} — {context}");
    }

    /// <summary>Todos os ecrãs encostados a outro por um lado (com algum comprimento em comum).</summary>
    private static bool Connected(LayoutRect[] rects)
    {
        static bool Touch(LayoutRect a, LayoutRect b)
        {
            var vertical = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
            var horizontal = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
            return ((a.Right == b.X || b.Right == a.X) && vertical > 0) || ((a.Bottom == b.Y || b.Bottom == a.Y) && horizontal > 0);
        }
        var seen = new HashSet<int> { 0 };
        var queue = new Queue<int>([0]);
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            for (var j = 0; j < rects.Length; j++)
                if (!seen.Contains(j) && Touch(rects[i], rects[j]))
                {
                    seen.Add(j);
                    queue.Enqueue(j);
                }
        }
        return seen.Count == rects.Length;
    }
}

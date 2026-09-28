namespace Triptico.Display;

public record struct LayoutRect(int X, int Y, int Width, int Height)
{
    public readonly int Right => X + Width;
    public readonly int Bottom => Y + Height;
    public readonly double CenterX => X + Width / 2.0;
    public readonly double CenterY => Y + Height / 2.0;

    public readonly bool Overlaps(LayoutRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
}

/// <summary>
/// Arruma os ecrãs para que fiquem encostados uns aos outros, sem buracos nem sobreposições.
/// Serve para quando um perfil desliga o ecrã do meio: o da direita encosta ao da esquerda.
/// </summary>
public static class LayoutMath
{
    public static LayoutRect[] Compact(IReadOnlyList<LayoutRect> wanted, int primaryIndex)
    {
        var result = wanted.ToArray();
        if (result.Length <= 1) return result;

        var placed = new List<int> { primaryIndex };

        // Os mais próximos do principal primeiro, para crescer a partir dele.
        var order = Enumerable.Range(0, result.Length)
            .Where(i => i != primaryIndex)
            .OrderBy(i => Distance(wanted[i], wanted[primaryIndex]))
            .ToList();

        foreach (var i in order)
        {
            var original = wanted[i];
            // Âncora: o ecrã já colocado que estava mais perto na disposição original.
            var anchorIdx = placed.OrderBy(p => Distance(original, wanted[p])).First();
            var anchorOriginal = wanted[anchorIdx];
            var anchor = result[anchorIdx];

            var side = SideOf(original, anchorOriginal);
            var r = original;
            switch (side)
            {
                case Side.Right:
                    r.X = anchor.Right;
                    r.Y = Clamp(anchor.Y + (original.Y - anchorOriginal.Y), anchor.Y - r.Height + 1, anchor.Bottom - 1);
                    break;
                case Side.Left:
                    r.X = anchor.X - r.Width;
                    r.Y = Clamp(anchor.Y + (original.Y - anchorOriginal.Y), anchor.Y - r.Height + 1, anchor.Bottom - 1);
                    break;
                case Side.Below:
                    r.Y = anchor.Bottom;
                    r.X = Clamp(anchor.X + (original.X - anchorOriginal.X), anchor.X - r.Width + 1, anchor.Right - 1);
                    break;
                case Side.Above:
                    r.Y = anchor.Y - r.Height;
                    r.X = Clamp(anchor.X + (original.X - anchorOriginal.X), anchor.X - r.Width + 1, anchor.Right - 1);
                    break;
            }

            // Se bater noutro ecrã já colocado, empurra na mesma direção até ficar livre.
            for (var guard = 0; guard < 16; guard++)
            {
                var hit = placed.Select(p => result[p]).Where(p => p.Overlaps(r)).ToList();
                if (hit.Count == 0) break;
                switch (side)
                {
                    case Side.Right: r.X = hit.Max(h => h.Right); break;
                    case Side.Left: r.X = hit.Min(h => h.X) - r.Width; break;
                    case Side.Below: r.Y = hit.Max(h => h.Bottom); break;
                    case Side.Above: r.Y = hit.Min(h => h.Y) - r.Height; break;
                }
            }

            result[i] = r;
            placed.Add(i);
        }

        // O principal fica sempre em (0,0).
        var dx = -result[primaryIndex].X;
        var dy = -result[primaryIndex].Y;
        for (var i = 0; i < result.Length; i++)
            result[i] = result[i] with { X = result[i].X + dx, Y = result[i].Y + dy };

        return result;
    }

    private enum Side { Right, Left, Below, Above }

    private static Side SideOf(LayoutRect r, LayoutRect anchor)
    {
        var dx = r.CenterX - anchor.CenterX;
        var dy = r.CenterY - anchor.CenterY;
        // Normalizar pelo tamanho para decidir se está mais "ao lado" ou mais "por cima/baixo".
        var nx = dx / ((r.Width + anchor.Width) / 2.0);
        var ny = dy / ((r.Height + anchor.Height) / 2.0);
        if (Math.Abs(nx) >= Math.Abs(ny))
            return dx >= 0 ? Side.Right : Side.Left;
        return dy >= 0 ? Side.Below : Side.Above;
    }

    private static double Distance(LayoutRect a, LayoutRect b)
    {
        var dx = a.CenterX - b.CenterX;
        var dy = a.CenterY - b.CenterY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int Clamp(int v, int min, int max) => max < min ? min : Math.Min(Math.Max(v, min), max);
}

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Triptico.Display;

namespace Triptico.Views;

public sealed record PreviewItem(LayoutRect Rect, string Label, bool Enabled, bool Primary);

/// <summary>Miniatura da disposição dos ecrãs: ligados a cheio, desligados tracejados.</summary>
public sealed class LayoutPreview : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IReadOnlyList<PreviewItem>), typeof(LayoutPreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<PreviewItem>? Items
    {
        get => (IReadOnlyList<PreviewItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var items = Items;
        if (items is null || items.Count == 0 || ActualWidth < 10 || ActualHeight < 10) return;

        var minX = items.Min(i => i.Rect.X);
        var minY = items.Min(i => i.Rect.Y);
        var maxX = items.Max(i => i.Rect.Right);
        var maxY = items.Max(i => i.Rect.Bottom);
        const double pad = 2;
        var scale = Math.Min((ActualWidth - 2 * pad) / Math.Max(1, maxX - minX), (ActualHeight - 2 * pad) / Math.Max(1, maxY - minY));
        var ox = (ActualWidth - (maxX - minX) * scale) / 2;
        var oy = (ActualHeight - (maxY - minY) * scale) / 2;

        var accent = Find("AccentFillColorDefaultBrush", SystemColors.AccentColorBrush);
        var onAccent = Find("TextOnAccentFillColorPrimaryBrush", Brushes.White);
        var muted = Find("TextFillColorTertiaryBrush", Brushes.Gray);
        var dash = new Pen(muted, 1.2) { DashStyle = new DashStyle([3, 3], 0) };
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var item in items.OrderBy(i => i.Enabled))
        {
            var r = new Rect(
                ox + (item.Rect.X - minX) * scale + 1.5,
                oy + (item.Rect.Y - minY) * scale + 1.5,
                Math.Max(1, item.Rect.Width * scale - 3),
                Math.Max(1, item.Rect.Height * scale - 3));

            if (item.Enabled) dc.DrawRoundedRectangle(accent, null, r, 3, 3);
            else dc.DrawRoundedRectangle(null, dash, r, 3, 3);

            var size = Math.Clamp(r.Height * 0.38, 9, 20);
            var text = new FormattedText(item.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, item.Primary ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal),
                size, item.Enabled ? onAccent : muted, dpi);
            if (text.Width < r.Width - 2)
                dc.DrawText(text, new Point(r.X + (r.Width - text.Width) / 2, r.Y + (r.Height - text.Height) / 2));
        }
    }

    private Brush Find(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    /// <summary>Monta a miniatura de um perfil, com os ecrãs desligados na última posição conhecida.</summary>
    public static IReadOnlyList<PreviewItem> For(Profiles.Profile profile, App app)
    {
        var map = DisplayManager.Resolve(profile.Monitors, app.Monitors);
        var rects = new List<(LayoutRect Rect, Profiles.ProfileMonitor Pm)>();
        foreach (var pm in profile.Monitors)
        {
            LayoutRect? rect = pm.HasPosition
                ? new LayoutRect(pm.X!.Value, pm.Y!.Value, pm.Width ?? 1920, pm.Height ?? 1080)
                : app.LastKnownRect(pm.Id);
            if (rect is null && map.TryGetValue(pm, out var live) && live.Active) rect = live.Bounds;
            if (rect is { } r) rects.Add((r, pm));
        }
        // Ecrãs que nunca estiveram ligados à vista da app: ficam à direita, com um tamanho típico.
        foreach (var pm in profile.Monitors.Where(pm => rects.All(x => x.Pm != pm)))
        {
            var right = rects.Count == 0 ? 0 : rects.Max(x => x.Rect.Right);
            var top = rects.Count == 0 ? 0 : rects.Min(x => x.Rect.Y);
            rects.Add((new LayoutRect(right, top, 1920, 1080), pm));
        }
        if (rects.Count == 0) return [];

        var primary = rects.FindIndex(x => x.Pm.Enabled && x.Pm.Primary);
        if (primary < 0) primary = Math.Max(0, rects.FindIndex(x => x.Pm.Enabled));
        var laid = LayoutMath.Compact(rects.Select(x => x.Rect).ToArray(), primary);

        return rects.Select((x, i) => new PreviewItem(
            laid[i],
            map.TryGetValue(x.Pm, out var m) ? app.MonitorNumber(m).ToString() : "–",
            x.Pm.Enabled,
            x.Pm.Enabled && x.Pm.Primary)).ToList();
    }
}

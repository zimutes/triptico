using System.Windows;
using System.Windows.Controls;

namespace Triptico.Views;

/// <summary>Caixa simples para pedir um texto.</summary>
public static class PromptWindow
{
    public static string? Ask(Window owner, string title, string message, string initial)
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Guardar", IsDefault = true, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0) };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        var cancel = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 96 };

        var window = new Window
        {
            Title = title,
            Owner = owner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24, 20, 24, 20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    box,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 18, 0, 0),
                        Children = { cancel, ok },
                    },
                },
            },
        };
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        return window.ShowDialog() == true ? box.Text : null;
    }
}

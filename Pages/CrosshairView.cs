using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;

namespace CS2Box.Pages;

/// <summary>可以在列表模板里绑定的小准星预览。</summary>
public class CrosshairView : Canvas
{
    public static readonly DependencyProperty CrosshairProperty =
        DependencyProperty.Register(nameof(Crosshair), typeof(Crosshair), typeof(CrosshairView),
            new PropertyMetadata(null, (d, _) => ((CrosshairView)d).Redraw()));

    public Crosshair? Crosshair
    {
        get => (Crosshair?)GetValue(CrosshairProperty);
        set => SetValue(CrosshairProperty, value);
    }

    public double Scale { get; set; } = 3;

    public CrosshairView()
    {
        Loaded += (_, _) => Redraw();
    }

    void Redraw()
    {
        if (double.IsNaN(Width) || double.IsNaN(Height)) return;
        if (Crosshair is { } x) CrosshairRenderer.Draw(this, x, Scale);
        else Children.Clear();
    }
}

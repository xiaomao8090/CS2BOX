using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CS2Box.Services;

namespace CS2Box.Pages;

/// <summary>在 Canvas 上画一个近似的准星预览（经典静态样式）。</summary>
public static class CrosshairRenderer
{
    public static void Draw(Canvas canvas, Crosshair x, double scale = 4)
    {
        canvas.Children.Clear();
        double cx = canvas.Width / 2, cy = canvas.Height / 2;

        // 游戏里粗细/间隙在 1080p 下大约是“值 × 2 像素”，这里按 scale 放大便于看清
        double len = x.Size * scale;
        double th = Math.Max(1, x.Thickness * scale * 0.5);
        double gap = Math.Max(0, (x.Gap + 4) * scale * 0.5);

        var rects = new List<Rect>
        {
            new(cx - th / 2, cy + gap, th, len),           // 下
            new(cx - gap - len, cy - th / 2, len, th),     // 左
            new(cx + gap, cy - th / 2, len, th),           // 右
        };
        if (!x.TStyle) rects.Add(new Rect(cx - th / 2, cy - gap - len, th, len)); // 上
        if (x.Dot) rects.Add(new Rect(cx - th / 2, cy - th / 2, th, th));

        var (r, g, b) = x.DisplayColor;
        byte a = x.UseAlpha ? (byte)x.Alpha : (byte)255;
        var fill = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        double o = x.Outline ? Math.Max(1, x.OutlineThickness * scale * 0.5) : 0;

        if (o > 0)
            foreach (var rc in rects) Add(canvas, new Rect(rc.X - o, rc.Y - o, rc.Width + o * 2, rc.Height + o * 2), new SolidColorBrush(Color.FromArgb(a, 0, 0, 0)));
        foreach (var rc in rects) Add(canvas, rc, fill);
    }

    static void Add(Canvas c, Rect r, Brush brush)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        var rect = new Rectangle { Width = r.Width, Height = r.Height, Fill = brush, SnapsToDevicePixels = true };
        Canvas.SetLeft(rect, r.X);
        Canvas.SetTop(rect, r.Y);
        c.Children.Add(rect);
    }
}

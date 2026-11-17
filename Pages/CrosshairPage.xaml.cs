using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CS2Box.Services;

namespace CS2Box.Pages;

public partial class CrosshairPage : Page
{
    // cl_crosshaircolor：0 红 1 绿 2 黄 3 蓝 4 青 5 自定义
    static readonly string[] ColorNames = ["红", "绿", "黄", "蓝", "青", "自定义"];

    bool _loading;

    public CrosshairPage()
    {
        InitializeComponent();

        for (int i = 0; i < ColorNames.Length; i++)
        {
            int id = i;
            var x = new Crosshair { Color = id, R = 255, G = 255, B = 255 };
            var (r, g, b) = x.DisplayColor;
            var swatch = new Border
            {
                Width = 28, Height = 28, Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(2), Tag = id, Cursor = Cursors.Hand, ToolTip = ColorNames[id],
                Background = id == 5
                    ? new LinearGradientBrush(Colors.OrangeRed, Colors.MediumPurple, 45)
                    : new SolidColorBrush(Color.FromRgb(r, g, b)),
            };
            swatch.MouseLeftButtonUp += (_, _) => { Settings.Current.Crosshair.Color = id; Load(); Save(); };
            ColorBox.Children.Add(swatch);
        }

        Loaded += (_, _) => Load();
    }

    /// <summary>把设置里的准星填到控件上。</summary>
    void Load()
    {
        _loading = true;
        var x = Settings.Current.Crosshair;
        StyleBox.SelectedIndex = Math.Clamp(x.Style, 0, 5);
        SSize.Value = x.Size;
        SThick.Value = x.Thickness;
        SGap.Value = x.Gap;
        Outline.IsChecked = x.Outline;
        SOutline.Value = x.OutlineThickness;
        UseAlpha.IsChecked = x.UseAlpha;
        SAlpha.Value = x.Alpha;
        SR.Value = x.R; SG.Value = x.G; SB.Value = x.B;
        Dot.IsChecked = x.Dot;
        TStyle.IsChecked = x.TStyle;
        Recoil.IsChecked = x.FollowRecoil;
        WeaponGap.IsChecked = x.GapUseWeaponValue;
        _loading = false;
        Render();
    }

    void Any_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        Save();
    }

    void Save()
    {
        var x = Settings.Current.Crosshair;
        x.Style = Math.Max(0, StyleBox.SelectedIndex);
        x.Size = Math.Round(SSize.Value, 1);
        x.Thickness = Math.Round(SThick.Value, 1);
        x.Gap = Math.Round(SGap.Value, 1);
        x.Outline = Outline.IsChecked == true;
        x.OutlineThickness = SOutline.Value;
        x.UseAlpha = UseAlpha.IsChecked == true;
        x.Alpha = (int)SAlpha.Value;
        x.R = (int)SR.Value; x.G = (int)SG.Value; x.B = (int)SB.Value;
        x.Dot = Dot.IsChecked == true;
        x.TStyle = TStyle.IsChecked == true;
        x.FollowRecoil = Recoil.IsChecked == true;
        x.GapUseWeaponValue = WeaponGap.IsChecked == true;
        Settings.SaveSoon();
        Render();
    }

    void Render()
    {
        var x = Settings.Current.Crosshair;
        foreach (Border b in ColorBox.Children)
            b.BorderBrush = (int)b.Tag == x.Color ? (Brush)FindResource("TextFillColorPrimaryBrush") : Brushes.Transparent;
        RgbPanel.Visibility = x.Color == 5 ? Visibility.Visible : Visibility.Collapsed;
        SAlpha.IsEnabled = x.UseAlpha;
        SOutline.IsEnabled = x.Outline;

        CrosshairRenderer.Draw(Preview, x, 6);
        Cmd.Text = x.CommandLine;
    }

    void Import_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Settings.Current.Crosshair = Crosshair.FromShareCode(Code.Text);
        Settings.Save();
        Load();
        Toast.Ok("已导入分享码");
    });

    void Export_Click(object sender, RoutedEventArgs e)
    {
        Code.Text = Settings.Current.Crosshair.ToShareCode();
        Clipboard.SetText(Code.Text);
        Toast.Ok("分享码已生成并复制");
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Cmd.Text);
        Toast.Ok("指令已复制，粘贴到游戏控制台即可");
    }

    void Write_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Autoexec.WriteBlock("crosshair", Settings.Current.Crosshair.Commands());
        Toast.Ok("已写入 autoexec.cfg，启动项里勾上“执行 autoexec.cfg”才会自动生效");
    });
}

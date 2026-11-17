using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace CS2Box.Pages;

public partial class ProsPage : Page
{
    ProData _data = new();

    public ProsPage()
    {
        InitializeComponent();
        MyDpi.Text = Settings.Current.MyDpi.ToString();
        Loaded += (_, _) => Load();
    }

    void Load()
    {
        _data = Pros.Load();
        Sub.Text = $"数据来自 {_data.Source}，更新于 {_data.Updated}" + (Pros.IsCustomized ? "（已被你修改过）" : "") +
                   "。准星只收录了能确认数值的，其余可以点“填准星分享码”补上。灵敏度会按你的 DPI 换算成相同 eDPI。";
        Filter();
    }

    void Filter()
    {
        var q = Search.Text?.Trim() ?? "";
        List.ItemsSource = null;
        List.ItemsSource = _data.Players.Where(p => q == "" ||
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Team.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    void Search_TextChanged(object sender, TextChangedEventArgs e) => Filter();

    void MyDpi_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(MyDpi.Text, out var dpi) && dpi is >= 100 and <= 32000)
        {
            Settings.Current.MyDpi = dpi;
            Settings.SaveSoon();
        }
    }

    static ProPlayer? From(object sender) => (sender as FrameworkElement)?.Tag as ProPlayer;

    void ApplyCrosshair_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { Crosshair: { } x } p) return;
        Toast.Try(() =>
        {
            Settings.Current.Crosshair = x.Clone();
            Settings.Save();
            Autoexec.WriteBlock("crosshair", x.Commands());
            Toast.Ok($"已使用 {p.Name} 的准星并写入 autoexec.cfg");
        });
    }

    async void ApplyMouse_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { } p) return;
        var dpi = Settings.Current.MyDpi;
        var lines = p.MouseAndViewmodelCommands(dpi).ToList();

        var box = new MessageBox
        {
            Title = $"使用 {p.Name} 的灵敏度和视角",
            Content = $"按你的 DPI {dpi} 换算，写入 autoexec.cfg：\n\n" + string.Join("\n", lines),
            PrimaryButtonText = "写入",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            Autoexec.WriteBlock("mouse", lines);
            Toast.Ok("已写入 autoexec.cfg");
        });
    }

    async void ApplyRes_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { } p) return;
        var acc = MainWindow.CurrentAccount();
        if (acc == null) { Toast.Error("没有检测到 Steam 账号"); return; }
        if (!VideoConfig.TryParseRes(p.Resolution, out var w, out var h)) { Toast.Error("这条数据的分辨率格式不对"); return; }

        var box = new MessageBox
        {
            Title = "修改分辨率",
            Content = $"把 {acc.PersonaName} 的游戏分辨率改成 {w}x{h}（{p.Aspect}）？" +
                      (p.Scaling == "拉伸" ? "\n\n拉伸还需要在显卡控制面板里把缩放设为“全屏”。" : ""),
            PrimaryButtonText = "修改",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            VideoConfig.ApplyResolution(acc, w, h);
            Toast.Ok($"已改为 {w}x{h}，下次启动游戏生效");
        });
    }

    async void SetCode_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { } p) return;
        var input = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = "CSGO-xxxxx-xxxxx-xxxxx-xxxxx-xxxxx",
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Text = p.Crosshair?.ToShareCode() ?? "",
            MinWidth = 360,
        };
        var box = new MessageBox
        {
            Title = $"{p.Name} 的准星分享码",
            Content = input,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            p.Crosshair = Crosshair.FromShareCode(input.Text);
            Pros.Save(_data);
            Load();
            Toast.Ok($"已保存 {p.Name} 的准星");
        });
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is { } p) Toast.Try(() => Steam.OpenUrl(p.Url));
    }

    void Edit_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Steam.SelectInExplorer(Pros.EditablePath);
        Toast.Info("改完 pros.json 后回到这一页会重新读取");
    });

    async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!Pros.IsCustomized) { Toast.Info("当前就是自带数据"); return; }
        var box = new MessageBox
        {
            Title = "恢复自带数据",
            Content = "你填过的分享码和改动会丢失，确定吗？",
            PrimaryButtonText = "恢复",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;
        Toast.Try(() => { Pros.ResetToBundled(); Load(); Toast.Ok("已恢复"); });
    }
}

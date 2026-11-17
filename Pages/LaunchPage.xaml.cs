using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace CS2Box.Pages;

public partial class LaunchPage : Page
{
    static readonly (string Arg, string Title, string Desc)[] Catalog =
    [
        ("-novid", "跳过开场动画", "-novid"),
        ("-high", "高优先级", "-high · 以高 CPU 优先级运行"),
        ("-console", "打开控制台", "-console"),
        ("+fps_max 0", "不限帧率", "+fps_max 0"),
        ("-fullscreen", "独占全屏", "-fullscreen"),
        ("-nojoy", "禁用手柄", "-nojoy"),
        ("-allow_third_party_software", "允许第三方捕获", "-allow_third_party_software · OBS 游戏捕获需要"),
        ("+exec autoexec", "执行 autoexec.cfg", "+exec autoexec"),
    ];

    bool _loading;

    public LaunchPage()
    {
        InitializeComponent();
        _loading = true;
        foreach (var (arg, title, desc) in Catalog)
        {
            var sw = new ToggleSwitch { IsChecked = Settings.Current.LaunchOptions.Contains(arg), Tag = arg };
            sw.Checked += Toggle_Changed;
            sw.Unchecked += Toggle_Changed;
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = title });
            header.Children.Add(new TextBlock { Text = desc, FontSize = 12, Opacity = 0.7 });
            Options.Children.Add(new CardControl { Header = header, Content = sw, Margin = new Thickness(0, 0, 0, 4) });
        }
        Extra.Text = Settings.Current.ExtraArgs;
        _loading = false;
        Result.Text = BuildArgs();
        Loaded += (_, _) => ShowSteamCurrent();
    }

    void ShowSteamCurrent()
    {
        try
        {
            var acc = MainWindow.CurrentAccount();
            var cur = acc != null ? Steam.ReadSteamLaunchOptions(acc) : null;
            SteamCurrent.Text = acc == null ? "" : $"{acc.PersonaName} 在 Steam 里的启动选项：" + (string.IsNullOrEmpty(cur) ? "（空）" : cur);
        }
        catch { SteamCurrent.Text = ""; }
    }

    async void WriteSteam_Click(object sender, RoutedEventArgs e)
    {
        var acc = MainWindow.CurrentAccount();
        if (acc == null) { Toast.Error("没有检测到 Steam 账号"); return; }

        var running = Steam.IsSteamRunning;
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "写入 Steam 启动选项",
            Content = $"把 {acc.PersonaName} 的 CS2 启动选项改成：\n{BuildArgs()}\n\n" +
                      (running ? "Steam 正在运行，需要先关闭它再写入，写完会重新打开。" : "原文件会备份为 localconfig.vdf.cs2box.bak。"),
            PrimaryButtonText = running ? "关闭 Steam 并写入" : "写入",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        await Toast.Try(async () =>
        {
            if (running)
            {
                Toast.Info("正在关闭 Steam…");
                await Steam.ShutdownSteamAsync();
            }
            Steam.WriteSteamLaunchOptions(acc, BuildArgs());
            if (running) Steam.StartSteam();
            Toast.Ok("已写入 Steam 启动选项");
            ShowSteamCurrent();
        });
    }

    public static string BuildArgs()
    {
        var s = Settings.Current;
        var parts = Catalog.Select(c => c.Arg).Where(s.LaunchOptions.Contains).ToList();
        if (!string.IsNullOrWhiteSpace(s.ExtraArgs)) parts.Add(s.ExtraArgs.Trim());
        return string.Join(" ", parts);
    }

    void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not ToggleSwitch { Tag: string arg } sw) return;
        var list = Settings.Current.LaunchOptions;
        list.Remove(arg);
        if (sw.IsChecked == true) list.Add(arg);
        Save();
    }

    void Extra_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        Settings.Current.ExtraArgs = Extra.Text;
        Save();
    }

    void Save()
    {
        Settings.SaveSoon();
        Result.Text = BuildArgs();
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Result.Text);
        Toast.Ok("已复制，可以粘贴到 Steam 的启动选项里");
    }

    void Launch_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Steam.LaunchCs2(BuildArgs());
        Toast.Ok("已通知 Steam 启动 CS2");
    });
}

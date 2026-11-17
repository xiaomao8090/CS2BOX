using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;

namespace CS2Box.Pages;

public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    void Refresh()
    {
        var cs2 = Steam.Cs2Path;
        GamePath.Text = cs2 ?? "未找到安装目录（可在设置里指定 Steam 路径）";
        LaunchBtn.IsEnabled = cs2 != null;

        var args = LaunchPage.BuildArgs();
        ArgsLine.Text = string.IsNullOrEmpty(args) ? "无启动参数" : "启动参数  " + args;
        LaunchDesc.Text = string.IsNullOrEmpty(args) ? "未设置" : args;

        CacheSize.Text = Steam.FormatSize(Steam.DirSize(Steam.ShaderCacheDir));

        var acc = MainWindow.CurrentAccount();
        AccNick.Text = acc?.PersonaName ?? "—";
        AccUser.Text = acc?.AccountName ?? "—";
        AccId.Text = acc?.SteamId64 ?? "—";
        AccLast.Text = acc != null ? acc.LastLogin.ToString("yyyy-MM-dd HH:mm") : "—";
        AccRemember.Text = acc == null ? "—" : acc.RememberPassword ? "是" : "否";
        AccCfg.Text = acc != null && Steam.UserCfgDir(acc) is { } d && Directory.Exists(d) ? "已找到" : "未找到";
    }

    void Launch_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Steam.LaunchCs2(LaunchPage.BuildArgs());
        Toast.Ok("已通知 Steam 启动 CS2");
        if (Settings.Current.MinimizeOnLaunch && MainWindow.Instance != null)
            MainWindow.Instance.WindowState = WindowState.Minimized;
    });

    async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var dir = Steam.ShaderCacheDir;
        if (dir == null || !Directory.Exists(dir)) { Toast.Info("没有着色器缓存"); return; }

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "清理着色器缓存",
            Content = $"将删除 {dir}\n下次进游戏会重新编译着色器，首次加载会慢一些。",
            PrimaryButtonText = "清理",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        await Toast.Try(async () =>
        {
            var size = Steam.DirSize(dir);
            await Task.Run(() => Directory.Delete(dir, true));
            Toast.Ok($"已清理 {Steam.FormatSize(size)}");
            Refresh();
        });
    }

    void Validate_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        Steam.ValidateCs2();
        Toast.Info("已交给 Steam 验证，进度在 Steam 下载页查看");
    });

    void GoAccounts(object s, RoutedEventArgs e) => MainWindow.Go(typeof(AccountsPage));
    void GoLaunch(object s, RoutedEventArgs e) => MainWindow.Go(typeof(LaunchPage));
    void GoCrosshair(object s, RoutedEventArgs e) => MainWindow.Go(typeof(CrosshairPage));
    void GoConfig(object s, RoutedEventArgs e) => MainWindow.Go(typeof(ConfigPage));
}

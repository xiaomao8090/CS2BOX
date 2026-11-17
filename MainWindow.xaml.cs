using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CS2Box.Pages;
using CS2Box.Services;
using Wpf.Ui.Controls;

namespace CS2Box;

public partial class MainWindow : FluentWindow
{
    public static MainWindow? Instance { get; private set; }

    Tray? _tray;
    bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        Toast.Presenter = SnackbarPresenter;

        Loaded += async (_, _) =>
        {
            RootNav.Navigate(typeof(HomePage));
            RefreshProfile();
            _tray = new Tray(ShowFromTray, LaunchFromAnywhere, ExitApp);
            Hotkeys.Attach(new WindowInteropHelper(this).Handle, LaunchFromAnywhere);
            await CheckUpdateSilently();
        };
    }

    public static void Go(Type page) => Instance?.RootNav.Navigate(page);

    /// <summary>当前账号 = 注册表 AutoLoginUser 对应的账号，没有就取最近登录的。</summary>
    public static SteamAccount? CurrentAccount()
    {
        try
        {
            var list = Steam.LoadAccounts();
            var auto = Steam.CurrentAutoLoginUser;
            return list.FirstOrDefault(a => a.AccountName.Equals(auto, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
        }
        catch { return null; }
    }

    public void RefreshProfile()
    {
        var acc = CurrentAccount();
        ProfileName.Text = acc?.PersonaName ?? "未登录";
        ProfileUser.Text = acc?.AccountName ?? "未检测到 Steam 账号";
        ProfileInitial.Text = acc?.Initial ?? "?";

        var avatar = acc != null ? Steam.AvatarPath(acc) : null;
        if (avatar != null)
        {
            ProfileAvatar.Fill = new ImageBrush(new BitmapImage(new Uri(avatar))) { Stretch = Stretch.UniformToFill };
            ProfileAvatar.Visibility = Visibility.Visible;
        }
        else ProfileAvatar.Visibility = Visibility.Collapsed;
    }

    void Profile_Click(object sender, MouseButtonEventArgs e) => Go(typeof(AccountsPage));

    // ---------- 托盘 / 热键 ----------

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    void LaunchFromAnywhere()
    {
        try
        {
            Steam.LaunchCs2(LaunchPage.BuildArgs());
            if (IsVisible) Toast.Ok("已通知 Steam 启动 CS2");
        }
        catch (Exception ex)
        {
            ShowFromTray();
            Toast.Error(ex.Message);
        }
    }

    void ExitApp()
    {
        _exiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && Settings.Current.CloseToTray && _tray != null)
        {
            e.Cancel = true;
            Hide();
            _tray.TipOnce();
            return;
        }
        Hotkeys.Detach();
        _tray?.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }

    async Task CheckUpdateSilently()
    {
        if (string.IsNullOrWhiteSpace(Settings.Current.UpdateUrl)) return;
        try
        {
            if (await Updater.CheckAsync() is { } info)
                Toast.Info($"有新版本 {info.Version}，可在设置里下载");
        }
        catch { /* 启动时静默失败 */ }
    }
}

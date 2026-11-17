using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CS2Box.Services;
using Wpf.Ui.Controls;

namespace CS2Box.Pages;

public class AccountRow
{
    public required SteamAccount Account { get; init; }
    public bool IsCurrent { get; init; }

    public string Initial => Account.Initial;
    public string Detail => $"{Account.AccountName} · 上次登录 {Account.LastLogin:yyyy-MM-dd HH:mm}" +
                            (Account.RememberPassword ? "" : " · 未记住密码");
    public Visibility CurrentVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
    public bool CanSwitch => !IsCurrent;
    public string ActionText => IsCurrent ? "已登录" : "切换";
    public ControlAppearance ActionAppearance => IsCurrent ? ControlAppearance.Secondary : ControlAppearance.Primary;

    public Brush? AvatarBrush => Steam.AvatarPath(Account) is { } p
        ? new ImageBrush(new BitmapImage(new Uri(p))) { Stretch = Stretch.UniformToFill }
        : null;
}

public partial class AccountsPage : Page
{
    List<AccountRow> _rows = [];

    public AccountsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    void Load()
    {
        var cur = MainWindow.CurrentAccount();
        _rows = Steam.LoadAccounts()
            .Select(a => new AccountRow { Account = a, IsCurrent = a.SteamId64 == cur?.SteamId64 })
            .ToList();
        Filter();
        MainWindow.Instance?.RefreshProfile();
    }

    void Filter()
    {
        var q = Search.Text?.Trim() ?? "";
        var shown = _rows.Where(r => q == "" ||
            r.Account.PersonaName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            r.Account.AccountName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        List.ItemsSource = shown;
        Empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void Search_TextChanged(object sender, TextChangedEventArgs e) => Filter();
    void Refresh_Click(object sender, RoutedEventArgs e) { Load(); Toast.Info($"读取到 {_rows.Count} 个账号"); }

    async void Switch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SteamAccount acc) return;
        if (sender is System.Windows.Controls.Button b) b.IsEnabled = false;
        Toast.Info($"正在切换到 {acc.PersonaName}，Steam 会重启");
        await Toast.Try(async () =>
        {
            await Steam.SwitchAccountAsync(acc);
            Toast.Ok($"已切换到 {acc.PersonaName}");
        });
        Load();
    }

    /// <summary>清空自动登录并重启 Steam，用来登录一个新账号。</summary>
    async void AddNew_Click(object sender, RoutedEventArgs e)
    {
        await Toast.Try(async () =>
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam"))
                key.SetValue("AutoLoginUser", "");
            await Steam.ShutdownSteamAsync();
            Steam.StartSteam();
            Toast.Info("Steam 已重启到登录界面，登录时记得勾选“记住密码”");
        });
    }

    void OpenCfg_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SteamAccount acc) return;
        var dir = Steam.UserCfgDir(acc);
        if (dir == null || !Directory.Exists(dir)) { Toast.Info("这个账号还没有 CS2 配置"); return; }
        Toast.Try(() => Steam.OpenFolder(dir));
    }
}

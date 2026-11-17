using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace CS2Box.Pages;

public partial class ConfigPage : Page
{
    SteamAccount? _acc;

    public ConfigPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    void Load()
    {
        _acc = MainWindow.CurrentAccount();
        Sub.Text = _acc == null
            ? "没有检测到 Steam 账号。"
            : $"针对当前账号 {_acc.PersonaName}：按键、视频设置（userdata\\{_acc.AccountId}\\730\\local\\cfg）和 autoexec.cfg。";
        var list = Backup.List();
        List.ItemsSource = list;
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (FromAcc.ItemsSource == null)
        {
            // 只列出有 CS2 配置的账号作为来源
            var all = Steam.LoadAccounts();
            FromAcc.ItemsSource = all.Where(a => Steam.UserCfgDir(a) is { } d && Directory.Exists(d)).ToList();
            ToAcc.ItemsSource = all;
            FromAcc.SelectedItem = ((List<SteamAccount>)FromAcc.ItemsSource).FirstOrDefault(a => a.SteamId64 == _acc?.SteamId64)
                                   ?? ((List<SteamAccount>)FromAcc.ItemsSource).FirstOrDefault();
        }
    }

    async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (FromAcc.SelectedItem is not SteamAccount from || ToAcc.SelectedItem is not SteamAccount to)
        {
            Toast.Info("先选好两个账号");
            return;
        }
        if (from.SteamId64 == to.SteamId64) { Toast.Info("两个账号是同一个"); return; }
        if (Cs2Running) { Toast.Error("先关闭 CS2"); return; }

        var box = new MessageBox
        {
            Title = "复制设置",
            Content = $"用 {from.PersonaName} 的设置覆盖 {to.PersonaName}？\n{to.PersonaName} 现有的设置会先自动备份。",
            PrimaryButtonText = "复制",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            Backup.CopyBetween(from, to);
            Toast.Ok($"已复制到 {to.PersonaName}");
            Load();
        });
    }

    static bool Cs2Running => Process.GetProcessesByName("cs2").Length > 0;

    void Backup_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        if (_acc == null) throw new InvalidOperationException("没有检测到 Steam 账号");
        var name = string.IsNullOrWhiteSpace(BackupName.Text) ? "备份" : BackupName.Text.Trim();
        Backup.Create(_acc, name);
        Toast.Ok("备份完成");
        Load();
    });

    async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BackupInfo b || _acc == null) return;
        if (Cs2Running) { Toast.Error("先关闭 CS2，否则游戏退出时会把设置覆盖回去"); return; }

        var box = new MessageBox
        {
            Title = "还原配置",
            Content = $"用「{b.Name}」覆盖 {_acc.PersonaName} 当前的 CS2 设置？\n建议先备份一次当前设置。",
            PrimaryButtonText = "还原",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            Backup.Restore(b, _acc);
            Toast.Ok($"已还原「{b.Name}」");
        });
    }

    async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BackupInfo b) return;
        var box = new MessageBox
        {
            Title = "删除备份",
            Content = $"删除「{b.Name}」？删除后无法恢复。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;
        Toast.Try(() => { File.Delete(b.File); Load(); });
    }

    void OpenUser_Click(object sender, RoutedEventArgs e)
    {
        var dir = _acc != null ? Steam.UserCfgDir(_acc) : null;
        if (dir == null || !Directory.Exists(dir)) { Toast.Info("当前账号还没有 CS2 配置"); return; }
        Toast.Try(() => Steam.OpenFolder(dir));
    }

    void OpenGame_Click(object sender, RoutedEventArgs e)
    {
        if (Steam.Cs2CfgDir is not { } dir) { Toast.Info("没找到 CS2 安装目录"); return; }
        Toast.Try(() => Steam.OpenFolder(dir));
    }

    void OpenBackups_Click(object sender, RoutedEventArgs e) => Toast.Try(() => Steam.OpenFolder(Backup.Dir));
}

using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using Microsoft.Win32;

namespace CS2Box.Pages;

public partial class SettingsPage : Page
{
    bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        var s = Settings.Current;
        foreach (ComboBoxItem item in Theme.Items)
            if ((string)item.Tag == s.Theme) Theme.SelectedItem = item;
        Minimize.IsChecked = s.MinimizeOnLaunch;
        CloseToTray.IsChecked = s.CloseToTray;
        foreach (var o in Hotkeys.Options)
        {
            var item = new ComboBoxItem { Content = o.Name, Tag = o.Key };
            Hotkey.Items.Add(item);
            if (o.Key == s.Hotkey) Hotkey.SelectedItem = item;
        }
        if (Hotkey.SelectedItem == null) Hotkey.SelectedIndex = 0;
        UpdateUrl.Text = s.UpdateUrl ?? "";
        VersionText.Text = "版本 " + Updater.CurrentText;
        DataDir.Text = Settings.DataDir;
        RefreshPaths();
        _loading = false;
    }

    void CloseToTray_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Settings.Current.CloseToTray = CloseToTray.IsChecked == true;
        Settings.Save();
    }

    void Hotkey_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Hotkey.SelectedItem is not ComboBoxItem { Tag: string key }) return;
        if (!Hotkeys.Apply(key))
        {
            Toast.Error("这个快捷键被别的程序占用了，换一个试试");
            return;
        }
        Settings.Current.Hotkey = key;
        Settings.Save();
        Toast.Ok(key == "None" ? "已关闭快捷键" : $"按 {((ComboBoxItem)Hotkey.SelectedItem).Content} 启动 CS2");
    }

    void UpdateUrl_LostFocus(object sender, RoutedEventArgs e)
    {
        Settings.Current.UpdateUrl = string.IsNullOrWhiteSpace(UpdateUrl.Text) ? null : UpdateUrl.Text.Trim();
        Settings.Save();
    }

    void ShowEula_Click(object sender, RoutedEventArgs e) => new EulaWindow(readOnly: true).ShowDialog();

    async void Check_Click(object sender, RoutedEventArgs e)
    {
        UpdateUrl_LostFocus(sender, e);
        CheckBtn.IsEnabled = false;
        await Toast.Try(async () =>
        {
            var info = await Updater.CheckAsync();
            if (info == null) { Toast.Ok("已是最新版本"); return; }

            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = $"发现新版本 {info.Version}",
                Content = string.IsNullOrWhiteSpace(info.Notes) ? "是否打开下载页面？" : info.Notes,
                PrimaryButtonText = "下载",
                CloseButtonText = "以后再说",
            };
            if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary
                && Uri.TryCreate(info.Url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
                Steam.OpenUrl(u.ToString());
        });
        CheckBtn.IsEnabled = true;
    }

    void RefreshPaths()
    {
        SteamPath.Text = (Steam.SteamPath ?? "未找到") + (string.IsNullOrEmpty(Settings.Current.SteamPathOverride) ? "（自动检测）" : "（手动指定）");
        Cs2Path.Text = Steam.Cs2Path ?? "未找到";
    }

    void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Theme.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        Settings.Current.Theme = tag;
        Settings.Save();
        Settings.ApplyTheme();
    }

    void Minimize_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Settings.Current.MinimizeOnLaunch = Minimize.IsChecked == true;
        Settings.Save();
    }

    void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        Settings.Current.SteamPathOverride = null;
        Settings.Save();
        RefreshPaths();
        MainWindow.Instance?.RefreshProfile();
        Toast.Info(Steam.SteamPath == null ? "没有检测到 Steam" : "已使用自动检测的路径");
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择 Steam 安装目录" };
        if (dlg.ShowDialog() != true) return;
        if (!File.Exists(Path.Combine(dlg.FolderName, "steam.exe")))
        {
            Toast.Error("这个目录里没有 steam.exe");
            return;
        }
        Settings.Current.SteamPathOverride = dlg.FolderName;
        Settings.Save();
        RefreshPaths();
        MainWindow.Instance?.RefreshProfile();
        Toast.Ok("已保存");
    }

    void OpenCs2_Click(object sender, RoutedEventArgs e)
    {
        if (Steam.Cs2Path is not { } p) { Toast.Info("没找到 CS2 安装目录"); return; }
        Toast.Try(() => Steam.OpenFolder(p));
    }

    void OpenData_Click(object sender, RoutedEventArgs e) => Toast.Try(() => Steam.OpenFolder(Settings.DataDir));
}

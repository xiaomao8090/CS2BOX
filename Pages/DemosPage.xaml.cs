using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace CS2Box.Pages;

public record DemoFile(string Path, string Relative, long Size, DateTime Time)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Detail => $"{Time:yyyy-MM-dd HH:mm} · {Steam.FormatSize(Size)} · {Relative}";
}

/// <summary>列出 game\csgo 下的 .dem 文件（含 replays 子目录）。</summary>
public partial class DemosPage : Page
{
    List<DemoFile> _demos = [];

    public DemosPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    void Load()
    {
        var root = Steam.Cs2GameDir;
        if (root == null) { Sub.Text = "没找到 CS2 安装目录。"; _demos = []; }
        else
        {
            _demos = new[] { root, System.IO.Path.Combine(root, "replays") }
                .Where(Directory.Exists)
                .SelectMany(d => new DirectoryInfo(d).GetFiles("*.dem"))
                .Select(f => new DemoFile(f.FullName, System.IO.Path.GetRelativePath(root, f.FullName).Replace('\\', '/'), f.Length, f.LastWriteTime))
                .OrderByDescending(d => d.Time)
                .ToList();
            Sub.Text = $"共 {_demos.Count} 个录像，占用 {Steam.FormatSize(_demos.Sum(d => d.Size))}。删除会移到回收站。";
        }
        List.ItemsSource = _demos;
        Empty.Visibility = _demos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    static DemoFile? From(object sender) => (sender as FrameworkElement)?.Tag as DemoFile;

    void Play_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { } d) return;
        var cmd = $"playdemo \"{d.Relative}\"";
        if (Steam.IsCs2Running)
        {
            Clipboard.SetText(cmd);
            Toast.Info("游戏已在运行，指令已复制，粘贴到控制台即可播放");
        }
        else
        {
            Toast.Try(() =>
            {
                Steam.LaunchCs2($"{LaunchPage.BuildArgs()} +{cmd}");
                Toast.Ok("正在启动游戏并播放录像");
            });
        }
    }

    void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is { } d) Toast.Try(() => Steam.SelectInExplorer(d.Path));
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (From(sender) is not { } d) return;
        Toast.Try(() => { Steam.Recycle(d.Path); Load(); Toast.Ok("已移到回收站"); });
    }

    async void DeleteOld_Click(object sender, RoutedEventArgs e)
    {
        var old = _demos.Where(d => d.Time < DateTime.Now.AddDays(-30)).ToList();
        if (old.Count == 0) { Toast.Info("没有 30 天前的录像"); return; }

        var box = new MessageBox
        {
            Title = "清理旧录像",
            Content = $"把 {old.Count} 个录像（{Steam.FormatSize(old.Sum(d => d.Size))}）移到回收站？",
            PrimaryButtonText = "移到回收站",
            CloseButtonText = "取消",
        };
        if (await box.ShowDialogAsync() != MessageBoxResult.Primary) return;

        Toast.Try(() =>
        {
            foreach (var d in old) Steam.Recycle(d.Path);
            Load();
            Toast.Ok($"已清理 {old.Count} 个录像");
        });
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => Load();

    void OpenDir_Click(object sender, RoutedEventArgs e)
    {
        if (Steam.Cs2GameDir is { } d) Toast.Try(() => Steam.OpenFolder(d));
        else Toast.Info("没找到 CS2 安装目录");
    }
}

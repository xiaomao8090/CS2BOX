using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;

namespace CS2Box.Pages;

public partial class WorkshopPage : Page
{
    public WorkshopPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    async Task LoadAsync()
    {
        var items = Net.LocalWorkshopItems();
        Sub.Text = Steam.WorkshopDir == null
            ? "没找到 CS2 安装目录。"
            : $"本地已下载 {items.Count} 个物品，共 {Steam.FormatSize(items.Sum(i => i.Size))}。";
        List.ItemsSource = items;
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        try
        {
            await Net.FillWorkshopTitlesAsync(items);
            List.ItemsSource = null;
            List.ItemsSource = items;
        }
        catch { /* 拿不到标题就显示 id */ }
    }

    void Manage_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
        Steam.OpenUrl("steam://openurl/https://steamcommunity.com/my/myworkshopfiles/?appid=730&browsefilter=mysubscriptions"));

    void Page_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is WorkshopItem w)
            Toast.Try(() => Steam.OpenUrl($"steam://url/CommunityFilePage/{w.Id}"));
    }

    async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not WorkshopItem w) return;
        if (Steam.IsCs2Running) { Toast.Error("先关闭 CS2"); return; }
        await Toast.Try(async () =>
        {
            Steam.Recycle(w.Path);
            Toast.Ok($"已把「{w.Display}」移到回收站");
            await LoadAsync();
        });
    }

    async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    void OpenDir_Click(object sender, RoutedEventArgs e)
    {
        if (Steam.WorkshopDir is { } d && Directory.Exists(d)) Toast.Try(() => Steam.OpenFolder(d));
        else Toast.Info("没有工坊目录");
    }
}

using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;

namespace CS2Box.Pages;

public partial class PingPage : Page
{
    public PingPage() => InitializeComponent();

    async void Start_Click(object sender, RoutedEventArgs e)
    {
        StartBtn.IsEnabled = false;
        Busy.Visibility = Visibility.Visible;
        Status.Text = "正在获取节点列表…";
        List.ItemsSource = null;
        int done = 0;
        try
        {
            var progress = new Progress<PopResult>(_ => Status.Text = $"已测试 {++done} 个节点…");
            var results = await Net.PingPopsAsync(progress);
            List.ItemsSource = results;
            var best = results.FirstOrDefault(r => r.Ms != null);
            Status.Text = best == null ? "全部超时，可能是网络屏蔽了 ping" : $"最快：{best.Name} {best.MsText}";
        }
        catch (Exception ex)
        {
            Status.Text = "";
            Toast.Error("获取节点失败：" + ex.Message);
        }
        finally
        {
            StartBtn.IsEnabled = true;
            Busy.Visibility = Visibility.Collapsed;
        }
    }
}

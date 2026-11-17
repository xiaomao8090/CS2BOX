using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace CS2Box.Pages;

public partial class BindsPage : Page
{
    /// <summary>
    /// 只收录单一动作或附带非动作指令的绑定。
    /// 2024 年 8 月起 CS2 不再支持一个键触发多个移动/攻击动作（如跳投），所以这里没有。
    /// </summary>
    static readonly (string Id, string Title, string Desc, string Command)[] Templates =
    [
        ("walk_clear", "静步时清除弹孔", "按住静步的同时执行 r_cleardecals", "+sprint; r_cleardecals"),
        ("duck_clear", "蹲下时清除弹孔", "按住蹲的同时执行 r_cleardecals", "+duck; r_cleardecals"),
        ("voice", "开关语音", "toggle voice_modenable", "toggle voice_modenable"),
        ("volume", "一键调低音量", "在 0.1 和 0.5 之间切换", "toggle volume 0.1 0.5"),
        ("hands", "左右手切换", "switchhands", "switchhands"),
        ("ping", "标记点", "player_ping", "player_ping"),
        ("netgraph", "显示网络状态", "toggle cq_netgraph 0 1", "toggle cq_netgraph 0 1"),
        ("radar", "雷达缩放", "在 0.4 和 0.7 之间切换", "toggle cl_radar_scale 0.4 0.7"),
    ];

    static readonly Regex KeyRx = new(@"^[a-z0-9_]+$", RegexOptions.IgnoreCase);

    public BindsPage()
    {
        InitializeComponent();
        foreach (var t in Templates)
        {
            Settings.Current.Binds.TryGetValue(t.Id, out var key);
            var box = new TextBox { Width = 140, PlaceholderText = "按键", Text = key ?? "", Tag = t.Id };
            box.TextChanged += (_, _) =>
            {
                Settings.Current.Binds[t.Id] = box.Text.Trim().ToLowerInvariant();
                Settings.SaveSoon();
                Render();
            };
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = t.Title });
            header.Children.Add(new TextBlock { Text = t.Desc, FontSize = 12, Opacity = 0.7 });
            List.Children.Add(new CardControl { Header = header, Content = box, Margin = new Thickness(0, 0, 0, 4) });
        }
        Render();
    }

    static List<string> Lines()
    {
        var lines = new List<string>();
        foreach (var t in Templates)
            if (Settings.Current.Binds.TryGetValue(t.Id, out var key) && !string.IsNullOrWhiteSpace(key))
                lines.Add($"bind \"{key}\" \"{t.Command}\"");
        return lines;
    }

    void Render()
    {
        var lines = Lines();
        Preview.Text = lines.Count == 0 ? "（没有启用的绑定）" : string.Join("\n", lines);
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(string.Join("; ", Lines()));
        Toast.Ok("已复制");
    }

    void Write_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        var bad = Settings.Current.Binds.Values.Where(k => !string.IsNullOrEmpty(k) && !KeyRx.IsMatch(k)).ToList();
        if (bad.Count > 0) throw new FormatException($"按键名不对：{string.Join("、", bad)}");
        Autoexec.WriteBlock("binds", Lines());
        Toast.Ok(Lines().Count == 0 ? "已从 autoexec.cfg 移除绑定" : "已写入 autoexec.cfg");
    });
}

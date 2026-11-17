using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CS2Box.Services;

namespace CS2Box.Pages;

/// <summary>编辑 userdata\&lt;id&gt;\730\local\cfg\cs2_video.txt。只改认识的键，其余原样保留。</summary>
public partial class VideoPage : Page
{
    static readonly string[] CommonRes =
        ["1280x960", "1440x1080", "1024x768", "1280x1024", "1680x1050", "1920x1080", "2560x1440", "1280x720"];

    // 下拉框 ↔ cs2_video.txt 的键
    (ComboBox Box, string Key)[] Combos => [
        (Latency, "setting.r_low_latency"),
        (Shadow, "setting.videocfg_shadow_quality"),
        (Texture, "setting.videocfg_texture_detail"),
        (Shader, "setting.shaderquality"),
        (Particle, "setting.videocfg_particle_detail"),
        (Ao, "setting.videocfg_ao_detail"),
        (Fsr, "setting.videocfg_fsr_detail"),
    ];

    SteamAccount? _acc;
    VdfNode? _root;
    VdfNode? _cfg;

    public VideoPage()
    {
        InitializeComponent();
        foreach (var r in DisplayResolutions().Concat(CommonRes).Distinct()) Res.Items.Add(r);
        foreach (var hz in DisplayRefreshRates()) Refresh.Items.Add(hz.ToString());
        Loaded += (_, _) => Load();
    }

    void Load()
    {
        _acc = MainWindow.CurrentAccount();
        var file = _acc != null ? Steam.VideoFile(_acc) : null;
        if (file == null || !File.Exists(file))
        {
            Sub.Text = "没找到当前账号的 cs2_video.txt，先用这个账号进一次游戏。";
            IsEnabled = false;
            return;
        }
        IsEnabled = true;
        Sub.Text = $"当前账号 {_acc!.PersonaName}。修改前请先关闭游戏，保存时会自动备份原文件。";

        _root = Vdf.ParseFile(file);
        _cfg = _root.Children.Select(c => c.Node).FirstOrDefault() ?? throw new InvalidDataException("cs2_video.txt 格式不认识");

        Res.Text = $"{Get("setting.defaultres", "1920")}x{Get("setting.defaultresheight", "1080")}";
        UpdateAspect();

        Mode.SelectedIndex = Get("setting.fullscreen") == "1" ? 0 : Get("setting.nowindowborder") == "1" ? 1 : 2;

        int.TryParse(Get("setting.refreshrate_numerator", "60"), out var num);
        int.TryParse(Get("setting.refreshrate_denominator", "1"), out var den);
        Refresh.Text = (den > 0 ? Math.Round((double)num / den) : num).ToString();

        Vsync.IsChecked = Get("setting.mat_vsync") == "1";

        foreach (var (box, key) in Combos)
            box.SelectedIndex = int.TryParse(Get(key), out var v) ? Math.Clamp(v, 0, box.Items.Count - 1) : 0;

        var msaa = Get("setting.msaa_samples", "0");
        Msaa.SelectedItem = Msaa.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == msaa) ?? Msaa.Items[0];
    }

    string? Get(string key, string? fallback = null) => _cfg?.Str(key) ?? fallback;

    static bool TryParseRes(string text, out int w, out int h) => VideoConfig.TryParseRes(text, out w, out h);
    static (int Mode, string Name) Aspect(int w, int h) => VideoConfig.Aspect(w, h);

    void UpdateAspect()
    {
        AspectText.Text = TryParseRes(Res.Text, out var w, out var h) ? $"宽高比 {Aspect(w, h).Name}" : "格式：宽x高，例如 1280x960";
    }

    void Res_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Res.SelectedItem is string s) Res.Text = s;
        UpdateAspect();
    }

    void Res_LostFocus(object sender, RoutedEventArgs e) => UpdateAspect();

    void Save_Click(object sender, RoutedEventArgs e) => Toast.Try(() =>
    {
        if (_cfg == null || _root == null || _acc == null) return;
        if (Steam.IsCs2Running) throw new InvalidOperationException("先关闭 CS2，游戏退出时会覆盖这个文件");
        if (!TryParseRes(Res.Text, out var w, out var h)) throw new FormatException("分辨率格式不对，例如 1280x960");
        if (!int.TryParse(Refresh.Text, out var hz) || hz is > 0 and < 30) throw new FormatException("刷新率要填数字，例如 144；0 表示跟随桌面");

        _cfg.Set("setting.defaultres", w.ToString());
        _cfg.Set("setting.defaultresheight", h.ToString());
        _cfg.Set("setting.aspectratiomode", Aspect(w, h).Mode.ToString());
        _cfg.Set("setting.fullscreen", Mode.SelectedIndex == 0 ? "1" : "0");
        _cfg.Set("setting.nowindowborder", Mode.SelectedIndex == 1 ? "1" : "0");
        _cfg.Set("setting.refreshrate_numerator", hz.ToString());
        _cfg.Set("setting.refreshrate_denominator", hz == 0 ? "0" : "1");
        _cfg.Set("setting.mat_vsync", Vsync.IsChecked == true ? "1" : "0");
        foreach (var (box, key) in Combos) _cfg.Set(key, Math.Max(0, box.SelectedIndex).ToString());
        _cfg.Set("setting.msaa_samples", (string)((ComboBoxItem)Msaa.SelectedItem).Tag);

        var file = Steam.VideoFile(_acc)!;
        File.Copy(file, file + ".cs2box.bak", overwrite: true);
        Vdf.WriteFile(file, _root);
        Toast.Ok("已保存，下次启动游戏生效");
    });

    void Reload_Click(object sender, RoutedEventArgs e) { Load(); Toast.Info("已重新读取"); }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_acc != null && Steam.VideoFile(_acc) is { } f && File.Exists(f)) Toast.Try(() => Steam.SelectInExplorer(f));
    }

    // ---------- 读取显示器支持的模式 ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string? device, int mode, ref DEVMODE dm);

    static List<DEVMODE> Modes()
    {
        var list = new List<DEVMODE>();
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++) list.Add(dm);
        return list;
    }

    static IEnumerable<string> DisplayResolutions() =>
        Modes().Where(m => m.dmPelsWidth >= 1024)
               .OrderByDescending(m => m.dmPelsWidth).ThenByDescending(m => m.dmPelsHeight)
               .Select(m => $"{m.dmPelsWidth}x{m.dmPelsHeight}").Distinct();

    static IEnumerable<int> DisplayRefreshRates() =>
        Modes().Select(m => m.dmDisplayFrequency).Where(f => f >= 50).Distinct().OrderByDescending(f => f);
}

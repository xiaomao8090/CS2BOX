using System.Text.Json;
using Wpf.Ui.Appearance;

namespace CS2Box.Services;

public class AppSettings
{
    public string Theme { get; set; } = "System";           // System / Light / Dark
    public string? SteamPathOverride { get; set; }
    public List<string> LaunchOptions { get; set; } = ["-novid", "-high", "-console", "+fps_max 0"];
    public string ExtraArgs { get; set; } = "";
    public bool MinimizeOnLaunch { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public string Hotkey { get; set; } = "None";             // 见 Hotkeys.Options
    public string EulaVersion { get; set; } = "";            // 同意过的免责声明版本，空 = 没同意过
    public string? UpdateUrl { get; set; }                   // 指向 version.json 的地址，空 = 不检查

    public Crosshair Crosshair { get; set; } = new();
    public int MyDpi { get; set; } = 800;

    /// <summary>按键绑定：模板 id → 按键名，空表示不启用。</summary>
    public Dictionary<string, string> Binds { get; set; } = new();
}

public static class Settings
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CS2Box");

    static string FilePath => Path.Combine(DataDir, "settings.json");
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { Current = new(); }
    }

    public static void Save()
    {
        _timer?.Stop();
        Directory.CreateDirectory(DataDir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Opts));
        File.Move(tmp, FilePath, overwrite: true);
    }

    static System.Windows.Threading.DispatcherTimer? _timer;

    /// <summary>
    /// 延迟保存：拖滑块、打字这类高频改动用这个，停下 400ms 后才真正写盘。
    /// 退出时 <see cref="Flush"/> 会把没写的补上。
    /// </summary>
    public static void SaveSoon()
    {
        if (_timer == null)
        {
            _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _timer.Tick += (_, _) =>
            {
                try { Save(); } catch { /* 下次再存 */ }
            };
        }
        _timer.Stop();
        _timer.Start();
    }

    public static void Flush()
    {
        if (_timer is { IsEnabled: true })
            try { Save(); } catch { }
    }

    public static void ApplyTheme()
    {
        switch (Current.Theme)
        {
            case "Light": ApplicationThemeManager.Apply(ApplicationTheme.Light); break;
            case "Dark": ApplicationThemeManager.Apply(ApplicationTheme.Dark); break;
            default: ApplicationThemeManager.ApplySystemTheme(); break;
        }
    }
}

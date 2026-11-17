using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2Box.Services;

public class ProPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Team { get; set; } = "";
    public int Dpi { get; set; }
    public double Sensitivity { get; set; }
    public double ZoomSensitivity { get; set; } = 1;
    public string Resolution { get; set; } = "";
    public string Aspect { get; set; } = "";
    public string Scaling { get; set; } = "";
    public string Viewmodel { get; set; } = "";
    public Crosshair? Crosshair { get; set; }

    [JsonIgnore] public double EDpi => Dpi * Sensitivity;
    [JsonIgnore] public string Url => $"https://prosettings.net/players/{Id}/";

    [JsonIgnore] public bool HasCrosshair => Crosshair != null;
    [JsonIgnore] public string Detail => $"DPI {Dpi} · 灵敏度 {F(Sensitivity)} · eDPI {F(EDpi)} · {Resolution} {Aspect} {Scaling}";

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>按自己的 DPI 换算成同样手感（同 eDPI）的游戏灵敏度。</summary>
    public double SensitivityFor(int myDpi) => myDpi > 0 ? Math.Round(EDpi / myDpi, 3) : Sensitivity;

    public IEnumerable<string> MouseAndViewmodelCommands(int myDpi)
    {
        yield return $"sensitivity {F(SensitivityFor(myDpi))}";
        yield return $"zoom_sensitivity_ratio {F(ZoomSensitivity)}";
        foreach (var c in Viewmodel.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return c;
    }
}

public class ProData
{
    public string Source { get; set; } = "";
    public string Updated { get; set; } = "";
    public List<ProPlayer> Players { get; set; } = [];
}

/// <summary>
/// 职业选手数据：自带一份（Data\pros.json），用户改过就存到 %AppData%\CS2Box\pros.json 并优先使用。
/// </summary>
public static class Pros
{
    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static string BundledPath => Path.Combine(AppContext.BaseDirectory, "Data", "pros.json");
    static string UserPath => Path.Combine(Settings.DataDir, "pros.json");

    public static bool IsCustomized => File.Exists(UserPath);

    public static ProData Load()
    {
        // 用户改过的 > exe 旁边的 Data > 打进 exe 里的那份（单文件发布时就靠它）
        string? json = null;
        if (File.Exists(UserPath)) json = File.ReadAllText(UserPath);
        else if (File.Exists(BundledPath)) json = File.ReadAllText(BundledPath);
        else json = ReadEmbedded();

        if (string.IsNullOrWhiteSpace(json)) return new ProData();
        return JsonSerializer.Deserialize<ProData>(json, Opts) ?? new ProData();
    }

    static string? ReadEmbedded()
    {
        var asm = typeof(Pros).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("pros.json", StringComparison.OrdinalIgnoreCase));
        if (name == null) return null;
        using var s = asm.GetManifestResourceStream(name);
        if (s == null) return null;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static void Save(ProData data)
    {
        Directory.CreateDirectory(Settings.DataDir);
        File.WriteAllText(UserPath, JsonSerializer.Serialize(data, Opts));
    }

    public static void ResetToBundled()
    {
        if (File.Exists(UserPath)) File.Delete(UserPath);
    }

    public static string EditablePath
    {
        get
        {
            if (!File.Exists(UserPath)) Save(Load());
            return UserPath;
        }
    }
}

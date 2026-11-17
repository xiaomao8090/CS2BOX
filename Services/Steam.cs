using System.Diagnostics;
using Microsoft.Win32;

namespace CS2Box.Services;

public record SteamAccount(string SteamId64, string AccountName, string PersonaName, DateTime LastLogin, bool RememberPassword)
{
    public uint AccountId => (uint)(ulong.Parse(SteamId64) - 76561197960265728UL);
    public string Initial => string.IsNullOrEmpty(PersonaName) ? "?" : PersonaName[..1].ToUpperInvariant();
    public string Display => $"{PersonaName}（{AccountName}）";
}

/// <summary>读取本机 Steam / CS2 的安装信息，以及账号切换、启动游戏。</summary>
public static class Steam
{
    public const string Cs2AppId = "730";

    public static string? SteamPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Settings.Current.SteamPathOverride)) return Settings.Current.SteamPathOverride;
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var p = key?.GetValue("SteamPath") as string;
            return string.IsNullOrEmpty(p) ? null : Path.GetFullPath(p);
        }
    }

    public static string? SteamExe => SteamPath is { } p ? Path.Combine(p, "steam.exe") : null;

    /// <summary>所有 Steam 库目录（含默认库）。</summary>
    public static List<string> Libraries()
    {
        var libs = new List<string>();
        if (SteamPath is not { } steam) return libs;
        libs.Add(steam);
        var lf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(lf))
        {
            try
            {
                var root = Vdf.ParseFile(lf).Node("libraryfolders");
                if (root != null)
                    foreach (var (_, v) in root.Children)
                        if (v.Str("path") is { } path) libs.Add(Path.GetFullPath(path));
            }
            catch { /* 文件损坏时只用默认库 */ }
        }
        return libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>CS2 所在的库目录。</summary>
    public static string? Cs2Library =>
        Libraries().FirstOrDefault(lib =>
            Directory.Exists(Path.Combine(lib, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo")));

    public static string? Cs2Path => Cs2Library is { } lib ? Path.Combine(lib, "steamapps", "common", "Counter-Strike Global Offensive") : null;
    public static string? Cs2GameDir => Cs2Path is { } p ? Path.Combine(p, "game", "csgo") : null;
    public static string? Cs2CfgDir => Cs2GameDir is { } g ? Path.Combine(g, "cfg") : null;

    public static string? ShaderCacheDir => Cs2Library is { } lib ? Path.Combine(lib, "steamapps", "shadercache", Cs2AppId) : null;
    public static string? WorkshopDir => Cs2Library is { } lib ? Path.Combine(lib, "steamapps", "workshop", "content", Cs2AppId) : null;

    /// <summary>userdata/&lt;id&gt;/730/local/cfg，存按键和视频设置。</summary>
    public static string? UserCfgDir(SteamAccount acc) =>
        SteamPath is { } p ? Path.Combine(p, "userdata", acc.AccountId.ToString(), Cs2AppId, "local", "cfg") : null;

    public static string? VideoFile(SteamAccount acc) => UserCfgDir(acc) is { } d ? Path.Combine(d, "cs2_video.txt") : null;

    public static string? LocalConfig(SteamAccount acc) =>
        SteamPath is { } p ? Path.Combine(p, "userdata", acc.AccountId.ToString(), "config", "localconfig.vdf") : null;

    public static string? AvatarPath(SteamAccount acc)
    {
        if (SteamPath is not { } p) return null;
        var f = Path.Combine(p, "config", "avatarcache", acc.SteamId64 + ".png");
        return File.Exists(f) ? f : null;
    }

    public static string? CurrentAutoLoginUser
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("AutoLoginUser") as string;
        }
    }

    // 账号列表缓存：loginusers.vdf 没变就不重新解析
    static string? _cacheFile;
    static DateTime _cacheStamp;
    static List<SteamAccount>? _cache;

    public static List<SteamAccount> LoadAccounts()
    {
        if (SteamPath is not { } p) return [];
        var file = Path.Combine(p, "config", "loginusers.vdf");
        if (!File.Exists(file)) return [];

        var stamp = File.GetLastWriteTimeUtc(file);
        if (_cache != null && _cacheFile == file && _cacheStamp == stamp) return [.. _cache];

        var list = ParseAccounts(file);
        (_cache, _cacheFile, _cacheStamp) = (list, file, stamp);
        return [.. list];
    }

    static List<SteamAccount> ParseAccounts(string file)
    {
        var list = new List<SteamAccount>();
        var users = Vdf.ParseFile(file).Node("users");
        if (users == null) return list;
        foreach (var (id, u) in users.Children)
        {
            long.TryParse(u.Str("Timestamp"), out var ts);
            list.Add(new SteamAccount(
                id,
                u.Str("AccountName") ?? "",
                u.Str("PersonaName") ?? "",
                DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime,
                u.Str("RememberPassword") == "1"));
        }
        return list.OrderByDescending(a => a.LastLogin).ToList();
    }

    public static bool IsSteamRunning => Process.GetProcessesByName("steam").Length > 0;
    public static bool IsCs2Running => Process.GetProcessesByName("cs2").Length > 0;

    /// <summary>
    /// 切换账号：写入 AutoLoginUser 后重启 Steam。
    /// 只对勾选过“记住密码”的账号有效，否则 Steam 会要求重新登录。
    /// </summary>
    public static async Task SwitchAccountAsync(SteamAccount acc)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam"))
        {
            key.SetValue("AutoLoginUser", acc.AccountName);
            key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
        }
        await ShutdownSteamAsync();
        StartSteam();
    }

    /// <summary>
    /// 请 Steam 正常退出并等它关掉。不会强杀：Steam 退出时要写 loginusers.vdf 和 localconfig.vdf，
    /// 中途杀掉可能把这两个文件写坏。等不到就抛异常，让用户手动退出。
    /// </summary>
    public static async Task ShutdownSteamAsync()
    {
        if (!IsSteamRunning || SteamExe is not { } exe) return;
        Process.Start(new ProcessStartInfo(exe, "-shutdown") { UseShellExecute = true });
        for (int i = 0; i < 120 && IsSteamRunning; i++) await Task.Delay(250);
        if (IsSteamRunning)
            throw new TimeoutException("Steam 30 秒内没有退出，请在 Steam 里手动退出后再试一次。");
        await Task.Delay(1000); // 进程消失后再给它一点时间落盘
    }

    public static void StartSteam(string args = "")
    {
        if (SteamExe is not { } exe || !File.Exists(exe)) throw new FileNotFoundException("找不到 steam.exe，请在设置里指定 Steam 路径。");
        Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
    }

    public static void LaunchCs2(string launchArgs) => StartSteam($"-applaunch {Cs2AppId} {launchArgs}".Trim());

    public static void ValidateCs2() => OpenUrl($"steam://validate/{Cs2AppId}");

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public static void SelectInExplorer(string file) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });

    public static long DirSize(string? dir)
    {
        if (dir == null || !Directory.Exists(dir)) return 0;
        return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };

    /// <summary>移到回收站，而不是直接删除。</summary>
    public static void Recycle(string path)
    {
        if (Directory.Exists(path))
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        else if (File.Exists(path))
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
    }

    /// <summary>
    /// 写入 Steam 里 CS2 的“启动选项”（localconfig.vdf）。
    /// Steam 运行时会在退出时覆盖这个文件，所以调用前要先关掉 Steam。
    /// </summary>
    public static void WriteSteamLaunchOptions(SteamAccount acc, string options)
    {
        var file = LocalConfig(acc) ?? throw new InvalidOperationException("找不到 Steam 路径");
        if (!File.Exists(file)) throw new FileNotFoundException("这个账号还没有 localconfig.vdf，先登录一次 Steam。");

        File.Copy(file, file + ".cs2box.bak", overwrite: true);
        var root = Vdf.ParseFile(file);
        var store = root.Node("UserLocalConfigStore") ?? throw new InvalidDataException("localconfig.vdf 格式不认识");
        var apps = store.Ensure("Software").Ensure("Valve").Ensure("Steam");
        var appsNode = apps.Node("apps") ?? apps.Ensure("apps");
        appsNode.Ensure(Cs2AppId).Set("LaunchOptions", options);
        Vdf.WriteFile(file, root);
    }

    public static string? ReadSteamLaunchOptions(SteamAccount acc)
    {
        var file = LocalConfig(acc);
        if (file == null || !File.Exists(file)) return null;
        return Vdf.ParseFile(file).Node("UserLocalConfigStore")?.Node("Software")?.Node("Valve")?.Node("Steam")
            ?.Node("apps")?.Node(Cs2AppId)?.Str("LaunchOptions");
    }
}

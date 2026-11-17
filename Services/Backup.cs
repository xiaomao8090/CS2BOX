using System.IO.Compression;

namespace CS2Box.Services;

public record BackupInfo(string File, string Name, DateTime Time, long Size)
{
    public string Detail => $"{Time:yyyy-MM-dd HH:mm} · {Steam.FormatSize(Size)}";
}

/// <summary>
/// 备份 / 还原 CS2 设置。zip 里分两个目录：
///   user/  → Steam\userdata\&lt;id&gt;\730\local\cfg （按键、视频、convar）
///   game/  → CS2\game\csgo\cfg 下的 autoexec.cfg
/// </summary>
public static class Backup
{
    public static string Dir => Path.Combine(Settings.DataDir, "backups");

    public static List<BackupInfo> List()
    {
        if (!Directory.Exists(Dir)) return [];
        return new DirectoryInfo(Dir).GetFiles("*.zip")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupInfo(f.FullName, Path.GetFileNameWithoutExtension(f.Name), f.LastWriteTime, f.Length))
            .ToList();
    }

    public static string Create(SteamAccount acc, string name)
    {
        var userCfg = Steam.UserCfgDir(acc);
        if (userCfg == null || !Directory.Exists(userCfg))
            throw new DirectoryNotFoundException($"没找到 {acc.PersonaName} 的 CS2 配置，先用这个账号进一次游戏。");

        Directory.CreateDirectory(Dir);
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var file = Path.Combine(Dir, $"{safe} {DateTime.Now:yyyyMMdd-HHmmss}.zip");

        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var f in Directory.GetFiles(userCfg))
            zip.CreateEntryFromFile(f, "user/" + Path.GetFileName(f));

        var autoexec = Steam.Cs2CfgDir is { } g ? Path.Combine(g, "autoexec.cfg") : null;
        if (autoexec != null && File.Exists(autoexec))
            zip.CreateEntryFromFile(autoexec, "game/autoexec.cfg");

        return file;
    }

    /// <summary>把 A 账号的按键、视频等设置复制给 B 账号。复制前先给 B 自动备份一次。</summary>
    public static void CopyBetween(SteamAccount from, SteamAccount to)
    {
        var src = Steam.UserCfgDir(from);
        var dst = Steam.UserCfgDir(to);
        if (src == null || !Directory.Exists(src)) throw new DirectoryNotFoundException($"{from.PersonaName} 还没有 CS2 配置");
        if (dst == null) throw new InvalidOperationException("找不到 Steam 路径");

        if (Directory.Exists(dst) && Directory.GetFiles(dst).Length > 0)
            Create(to, $"复制前自动备份-{to.PersonaName}");

        Directory.CreateDirectory(dst);
        // 文件名原样复制：CS2 的配置文件名（cs2_user_keys_0_slot0.vcfg 等）不含账号 id
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
    }

    public static void Restore(BackupInfo b, SteamAccount acc)
    {
        var userCfg = Steam.UserCfgDir(acc) ?? throw new InvalidOperationException("找不到 Steam 路径");
        Directory.CreateDirectory(userCfg);

        using var zip = ZipFile.OpenRead(b.File);
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name)) continue;
            string? target = null;
            if (e.FullName.StartsWith("user/")) target = Path.Combine(userCfg, e.Name);
            else if (e.FullName.StartsWith("game/") && Steam.Cs2CfgDir is { } g) target = Path.Combine(g, e.Name);
            if (target != null) e.ExtractToFile(target, overwrite: true);
        }
    }
}

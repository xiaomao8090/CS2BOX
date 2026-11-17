namespace CS2Box.Services;

/// <summary>cs2_video.txt 的通用操作。</summary>
public static class VideoConfig
{
    public static bool TryParseRes(string text, out int w, out int h)
    {
        w = h = 0;
        var parts = text.ToLowerInvariant().Replace("×", "x").Replace("*", "x").Split('x', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out w) && int.TryParse(parts[1], out h) && w >= 640 && h >= 480;
    }

    /// <summary>setting.aspectratiomode：0 = 4:3，1 = 16:9，2 = 16:10。</summary>
    public static (int Mode, string Name) Aspect(int w, int h)
    {
        double r = (double)w / h;
        if (Math.Abs(r - 4.0 / 3) < 0.02 || Math.Abs(r - 5.0 / 4) < 0.02) return (0, "4:3");
        if (Math.Abs(r - 16.0 / 10) < 0.02) return (2, "16:10");
        return (1, "16:9");
    }

    public static void ApplyResolution(SteamAccount acc, int w, int h)
    {
        if (Steam.IsCs2Running) throw new InvalidOperationException("先关闭 CS2，游戏退出时会覆盖视频设置");
        var file = Steam.VideoFile(acc);
        if (file == null || !File.Exists(file)) throw new FileNotFoundException("当前账号还没有 cs2_video.txt，先进一次游戏");

        var root = Vdf.ParseFile(file);
        var cfg = root.Children.Select(c => c.Node).FirstOrDefault() ?? throw new InvalidDataException("cs2_video.txt 格式不认识");
        cfg.Set("setting.defaultres", w.ToString());
        cfg.Set("setting.defaultresheight", h.ToString());
        cfg.Set("setting.aspectratiomode", Aspect(w, h).Mode.ToString());
        File.Copy(file, file + ".cs2box.bak", overwrite: true);
        Vdf.WriteFile(file, root);
    }
}

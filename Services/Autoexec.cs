using System.Text.RegularExpressions;

namespace CS2Box.Services;

/// <summary>
/// 往 autoexec.cfg 里写入带标记的区块，例如：
///   // >>> CS2Box crosshair
///   ...
///   // &lt;&lt;&lt; CS2Box crosshair
/// 只替换自己的区块，文件里其他内容原样保留。
/// </summary>
public static class Autoexec
{
    public static string? FilePath => Steam.Cs2CfgDir is { } d ? Path.Combine(d, "autoexec.cfg") : null;

    public static void WriteBlock(string name, IEnumerable<string> lines)
    {
        var file = FilePath ?? throw new DirectoryNotFoundException("没找到 CS2 安装目录");
        var begin = $"// >>> CS2Box {name}";
        var end = $"// <<< CS2Box {name}";
        var old = File.Exists(file) ? File.ReadAllText(file) : "";
        var body = string.Join("\n", lines);
        var block = $"{begin}\n{body}\n{end}";
        var pattern = Regex.Escape(begin) + "[\\s\\S]*?" + Regex.Escape(end);

        string text;
        if (Regex.IsMatch(old, pattern))
            text = string.IsNullOrWhiteSpace(body)
                ? Regex.Replace(old, pattern + "\\r?\\n?", "")
                : Regex.Replace(old, pattern, block.Replace("$", "$$"));
        else if (string.IsNullOrWhiteSpace(body))
            return;
        else
            text = (old.TrimEnd() + "\n\n" + block + "\n").TrimStart();

        File.WriteAllText(file, text);
    }

    public static string? ReadBlock(string name)
    {
        var file = FilePath;
        if (file == null || !File.Exists(file)) return null;
        var m = Regex.Match(File.ReadAllText(file),
            Regex.Escape($"// >>> CS2Box {name}") + "\\r?\\n([\\s\\S]*?)\\r?\\n" + Regex.Escape($"// <<< CS2Box {name}"));
        return m.Success ? m.Groups[1].Value : null;
    }
}

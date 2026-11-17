using System.Net.Http.Json;
using System.Reflection;

namespace CS2Box.Services;

/// <summary>
/// 检查更新。在设置里填一个 version.json 的地址，内容形如：
///   { "version": "0.2.0", "url": "https://.../CS2Box.zip", "notes": "更新内容" }
/// </summary>
public static class Updater
{
    public record UpdateInfo(string Version, string Url, string? Notes);

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Current.Build}";

    /// <summary>有新版本就返回信息，否则返回 null。没配置地址时抛异常。</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        var url = Settings.Current.UpdateUrl;
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("还没有填写更新地址");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new FormatException("更新地址需要是 https 开头的完整网址");

        var info = await Net.Http.GetFromJsonAsync<UpdateInfo>(uri,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("更新信息是空的");
        if (!Version.TryParse(info.Version, out var latest)) throw new InvalidDataException("版本号格式不对");
        return latest > Current ? info : null;
    }
}

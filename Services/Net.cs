using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace CS2Box.Services;

public class PopResult
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int? Ms { get; set; }
    public string MsText => Ms is { } m ? $"{m} ms" : "超时";
}

public record WorkshopItem(string Id, string Path, long Size)
{
    public string Title { get; set; } = "";
    public string Display => string.IsNullOrEmpty(Title) ? $"工坊物品 {Id}" : Title;
    public string Detail => $"{Id} · {Steam.FormatSize(Size)}";
}

/// <summary>访问 Valve 的公开接口（不需要登录、不发送任何个人信息）。</summary>
public static class Net
{
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static Net()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("CS2Box/0.1");
    }

    static readonly Dictionary<string, string> PopNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hkg"] = "香港", ["tyo"] = "东京", ["sgp"] = "新加坡", ["seo"] = "首尔", ["bom"] = "孟买", ["maa"] = "金奈",
        ["dxb"] = "迪拜", ["fra"] = "法兰克福", ["ams"] = "阿姆斯特丹", ["lhr"] = "伦敦", ["par"] = "巴黎",
        ["sto"] = "斯德哥尔摩", ["waw"] = "华沙", ["vie"] = "维也纳", ["mad"] = "马德里", ["hel"] = "赫尔辛基",
        ["iad"] = "美国东部（弗吉尼亚）", ["ord"] = "芝加哥", ["atl"] = "亚特兰大", ["dfw"] = "达拉斯",
        ["lax"] = "洛杉矶", ["sea"] = "西雅图", ["gru"] = "圣保罗", ["scl"] = "圣地亚哥", ["lim"] = "利马",
        ["syd"] = "悉尼", ["jnb"] = "约翰内斯堡",
    };

    /// <summary>取 Valve 数据中心列表，ping 每个节点的中继服务器。</summary>
    public static async Task<List<PopResult>> PingPopsAsync(IProgress<PopResult>? progress = null)
    {
        var json = await Http.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");
        using var doc = JsonDocument.Parse(json);
        var pops = doc.RootElement.GetProperty("pops");

        var tasks = new List<Task<PopResult>>();
        foreach (var pop in pops.EnumerateObject())
        {
            if (!pop.Value.TryGetProperty("relays", out var relays) || relays.GetArrayLength() == 0) continue;
            var ips = relays.EnumerateArray().Take(3)
                .Select(r => r.TryGetProperty("ipv4", out var ip) ? ip.GetString() : null)
                .OfType<string>().ToList();
            var desc = pop.Value.TryGetProperty("desc", out var d) ? d.GetString() ?? pop.Name : pop.Name;
            var name = PopNames.TryGetValue(pop.Name, out var cn) ? $"{cn} · {desc}" : Translate(desc);
            tasks.Add(PingOne(pop.Name, name, ips, progress));
        }
        var results = await Task.WhenAll(tasks);
        return results.OrderBy(r => r.Ms ?? int.MaxValue).ToList();
    }

    static readonly (string En, string Cn)[] Words =
    [
        ("Alibaba Cloud", "阿里云"), ("Tencent", "腾讯"), ("Guangzhou", "广州"), ("Shanghai", "上海"),
        ("Beijing", "北京"), ("Chengdu", "成都"), ("Wuhan", "武汉"), ("Tianjin", "天津"), ("Shenzhen", "深圳"),
        ("Hangzhou", "杭州"), ("Unicom", "联通"), ("Mobile", "移动"), ("Telecom", "电信"), (" (China)", ""), (" - ", " "),
    ];

    /// <summary>把国内节点的英文描述换成中文，例如 “Tencent Guangzhou - Unicom (China)” → “腾讯 广州 联通”。</summary>
    static string Translate(string desc)
    {
        if (!desc.Contains("(China)")) return desc;
        foreach (var (en, cn) in Words) desc = desc.Replace(en, cn);
        return desc.Trim();
    }

    static async Task<PopResult> PingOne(string code, string name, List<string> ips, IProgress<PopResult>? progress)
    {
        var result = new PopResult { Code = code, Name = name };
        var best = int.MaxValue;
        foreach (var ip in ips)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, 1500);
                    if (reply.Status == IPStatus.Success) best = Math.Min(best, (int)reply.RoundtripTime);
                }
                catch { }
            }
        }
        if (best != int.MaxValue) result.Ms = best;
        progress?.Report(result);
        return result;
    }

    /// <summary>本地已下载的创意工坊物品。</summary>
    public static List<WorkshopItem> LocalWorkshopItems()
    {
        var dir = Steam.WorkshopDir;
        if (dir == null || !Directory.Exists(dir)) return [];
        return Directory.GetDirectories(dir)
            .Select(d => new WorkshopItem(System.IO.Path.GetFileName(d), d, Steam.DirSize(d)))
            .OrderByDescending(i => i.Size)
            .ToList();
    }

    /// <summary>用公开接口查工坊物品标题。</summary>
    public static async Task FillWorkshopTitlesAsync(List<WorkshopItem> items)
    {
        if (items.Count == 0) return;
        var form = new List<KeyValuePair<string, string>> { new("itemcount", items.Count.ToString()) };
        for (int i = 0; i < items.Count; i++) form.Add(new($"publishedfileids[{i}]", items[i].Id));

        using var resp = await Http.PostAsync(
            "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
            new FormUrlEncodedContent(form));
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("response", out var r) || !r.TryGetProperty("publishedfiledetails", out var list)) return;
        foreach (var d in list.EnumerateArray())
        {
            var id = d.GetProperty("publishedfileid").GetString();
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item != null && d.TryGetProperty("title", out var t)) item.Title = t.GetString() ?? "";
        }
    }
}

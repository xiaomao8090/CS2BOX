using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CS2Box.Services;

/// <summary>认证结果：0 = 通过，其余见 <see cref="Describe"/>。</summary>
public record RealNameResult(int Code, string Desc)
{
    public bool Ok => Code == 0;
    public string Text => Describe(Code, Desc);

    public static string Describe(int code, string desc) => code switch
    {
        0 => "认证通过",
        1001 => "姓名格式不对（2~20 个字，和身份证一致）",
        1002 or 1006 => "短信验证码不对或已过期（60 秒）",
        1012 => "发太频繁，同一个号等一两分钟",
        1013 => "链接失效了，重新复制一条",
        1022 => "手机号格式不对",
        1029 => "图形验证码没过",
        1044 => "姓名和身份证对不上，换一套",
        111 => "试太多次了，换个链接",
        _ => string.IsNullOrWhiteSpace(desc) ? $"失败（{code}）" : $"{desc}（{code}）",
    };
}

/// <summary>
/// 蒸汽平台实名认证。走的是官网自己的接口：先取滑块题、本地算出缺口位置、
/// 按前端的算法签名提交，拿到短信验证码后再提交实名。
/// 都是 https 直连，不起浏览器。
/// </summary>
public static class RealName
{
    const string Rnr = "https://rnr.steamchina.com";
    const string Cap = "https://captchas.wanmei.com";
    const string Store = "https://store.steamchina.com";
    const string Redirect = "https://store.steamchina.com/login?agreementsource=2";
    const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        // 这几个站点的证书链在部分老机器上不完整，和其他模块一样放宽校验
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(Ua);
        return c;
    }

    // ---------------- 输入校验 ----------------

    static readonly Dictionary<int, string> Prov = new()
    {
        [11] = "北京", [12] = "天津", [13] = "河北", [14] = "山西", [15] = "内蒙古",
        [21] = "辽宁", [22] = "吉林", [23] = "黑龙江", [31] = "上海", [32] = "江苏",
        [33] = "浙江", [34] = "安徽", [35] = "福建", [36] = "江西", [37] = "山东",
        [41] = "河南", [42] = "湖北", [43] = "湖南", [44] = "广东", [45] = "广西",
        [46] = "海南", [50] = "重庆", [51] = "四川", [52] = "贵州", [53] = "云南",
        [54] = "西藏", [61] = "陕西", [62] = "甘肃", [63] = "青海", [64] = "宁夏",
        [65] = "新疆", [71] = "台湾", [81] = "香港", [82] = "澳门", [91] = "国外",
    };

    static readonly int[] Wk = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2];
    const string Chk = "10X98765432";

    /// <summary>从整条链接或裸 token 里取 32 位 token，取不到返回空。</summary>
    public static string ExtractToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return System.Text.RegularExpressions.Regex.Match(text.Trim(), "[0-9a-fA-F]{32}")
            .Value.ToLowerInvariant();
    }

    public static string? CheckName(string? n)
    {
        n = (n ?? "").Trim();
        if (n.Length == 0) return "姓名不能空";
        if (n.Any(char.IsWhiteSpace)) return "姓名里别带空格";
        if (n.Length is < 2 or > 20) return $"姓名 {n.Length} 个字，不对";
        return null;
    }

    public static string? CheckId(string? raw)
    {
        var s = (raw ?? "").Trim().ToUpperInvariant();
        if (s.Length != 18) return $"身份证要 18 位，你这是 {s.Length} 位";
        if (!s[..17].All(char.IsDigit)) return "前 17 位得是数字";
        if (!"0123456789X".Contains(s[17])) return "最后一位只能是数字或 X";
        if (s.Substring(14, 3) == "000") return "后三位是 000，不对";
        if (!Prov.ContainsKey(int.Parse(s[..2]))) return $"开头的 {s[..2]} 不是省份代码";
        int sum = 0;
        for (int i = 0; i < 17; i++) sum += (s[i] - '0') * Wk[i];
        if (s[17] != Chk[sum % 11]) return "校验位不对，肯定有一位填错了";
        if (!DateTime.TryParseExact(s.Substring(6, 8), "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "里面的生日不合法";
        return null;
    }

    public static string? CheckPhone(string? p)
    {
        p = (p ?? "").Trim();
        if (p.Length != 11 || !p.All(char.IsDigit) || p[0] != '1') return "手机号 11 位，1 开头";
        return null;
    }

    /// <summary>身份证里的生日、省份、性别、年龄。</summary>
    public static (DateTime Birth, string Prov, string Sex, int Age) IdInfo(string id)
    {
        id = id.Trim().ToUpperInvariant();
        var b = DateTime.ParseExact(id.Substring(6, 8), "yyyyMMdd", CultureInfo.InvariantCulture);
        int age = DateTime.Today.Year - b.Year;
        if (DateTime.Today < b.AddYears(age)) age--;
        var prov = Prov.TryGetValue(int.Parse(id[..2]), out var p) ? p : "?";
        var sex = (id[16] - '0') % 2 == 1 ? "男" : "女";
        return (b, prov, sex, age);
    }

    // ---------------- token ----------------

    /// <summary>token 能不能用：能提交就返回 true。1013 说明还没激活或者已经用掉。</summary>
    public static async Task<(bool Ok, string Code, string Desc)> CheckTokenAsync(string token)
    {
        var r = await PostJsonAsync($"{Rnr}/register?token={Uri.EscapeDataString(token)}", "{}");
        var code = r.GetProperty("code").GetInt32().ToString(CultureInfo.InvariantCulture);
        var desc = r.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        return (code != "1013", code, desc);
    }

    /// <summary>
    /// 刚拿到的链接直接提交会回 1013。先把协议页那几步走一遍，token 才算激活。
    /// </summary>
    public static async Task WarmAsync(string token)
    {
        var t = Uri.EscapeDataString(token);
        await GetStringAsync($"{Store}/agreements?token={t}");
        await GetStringAsync($"{Store}/agreements/startidverification?token={t}&redir={Uri.EscapeDataString(Redirect)}");
        await CompleteAgreementAsync(token);
    }

    /// <summary>网页上那个「完成」按钮，不点的话实名状态不落地。</summary>
    public static async Task<bool> CompleteAgreementAsync(string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{Store}/agreements/ajaxcompleteagreement")
            {
                Content = new FormUrlEncodedContent([new("token", token)]),
            };
            req.Headers.Referrer = new Uri($"{Store}/agreements?token={Uri.EscapeDataString(token)}");
            req.Headers.TryAddWithoutValidation("Origin", Store);
            req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            using var resp = await Http.SendAsync(req);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("success", out var s) && s.GetInt32() == 1;
        }
        catch { return false; }
    }

    // ---------------- 滑块 ----------------

    /// <summary>过滑块，返回 (capTicket, secCode, 距离)。失败抛异常。</summary>
    public static async Task<(string CapTicket, string SecCode, int Distance)> SolveCaptchaAsync(
        int rounds = 6, Action<string>? log = null)
    {
        string last = "未知";
        for (int n = 0; n < rounds; n++)
        {
            string cap, key;
            byte[] png;
            int[] loc;
            try
            {
                var init = await GetJsonAsync($"{Rnr}/initCaptcha");
                cap = init.GetProperty("result").GetProperty("capTicket").GetString() ?? "";

                var k = Unwrap(await GetStringAsync($"{Cap}/mCaptcha/key?appId=20012&capTicket={cap}&callback=jQuery1_1"));
                key = k.GetProperty("result").GetString() ?? "";

                var info = Unwrap(await GetStringAsync($"{Cap}/mCaptcha/info/{key}?callback=jQuery1_2")).GetProperty("result");
                loc = info.GetProperty("loc").EnumerateArray().Select(e => e.GetInt32()).ToArray();
                var imgUrl = info.GetProperty("img").GetString() ?? "";
                png = await Http.GetByteArrayAsync($"{imgUrl}?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            }
            catch (Exception ex)
            {
                last = "网络 " + ex.GetType().Name;
                await Task.Delay(800);
                continue;
            }

            // 一个 capKey 有三次机会，所以一次拿三个候选距离挨个试
            List<(int X, double Score)> cands;
            try { cands = Candidates(png, loc, 3); }
            catch (Exception ex)
            {
                last = ex.Message;
                log?.Invoke($"这道题解不开（{ex.Message}），换一道");
                continue;
            }

            foreach (var (dist, _) in cands)
            {
                var (code, msg, result) = await ValidateAsync(cap, key, dist);
                if (code == 0)
                    return (cap, result ?? "", dist);

                last = $"{code} {msg}";
                if (code is 111 or 105) break;   // 这个 capKey 废了 / 行为被判死
                await Task.Delay(300);
            }
            log?.Invoke($"第 {n + 1} 轮没过（{last}），换一道");
        }
        throw new InvalidOperationException($"滑块连试 {rounds} 轮没过：{last}");
    }

    static async Task<(int Code, string Msg, string? Result)> ValidateAsync(string cap, string key, int distance)
    {
        var key16 = DeriveKey(cap);
        var validData = Enc(
            $"{{\"length\":{distance},\"validateTimeMilSec\":{Random.Shared.Next(700, 1500)}}}", key16);
        var op = Enc(BuildTrack(distance), key16);

        var url = $"{Cap}/mCaptcha/validate?capKey={key}" +
                  $"&validData={Uri.EscapeDataString(validData)}" +
                  $"&op={Uri.EscapeDataString(op)}&fp=&label=1&callback=jQuery1_3";
        var doc = Unwrap(await GetStringAsync(url));
        int code = doc.TryGetProperty("code", out var c) ? c.GetInt32() : -99;
        var msg = doc.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        string? result = doc.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() : null;
        return (code, msg, result);
    }

    /// <summary>拼一条拖动轨迹，服务端会看这个字段。</summary>
    static string BuildTrack(int distance)
    {
        var sb = new StringBuilder("[");
        int n = Random.Shared.Next(26, 35);
        long t = 0;
        double x0 = 15, y0 = 60;
        sb.Append('[').Append(x0.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(y0.ToString(CultureInfo.InvariantCulture)).Append(",1,0]");
        for (int i = 1; i < n; i++)
        {
            double p = (double)i / n;
            double ease = p < 0.15 ? p * p * 3 : 1 - Math.Pow(1 - p, 2.4);
            t += Random.Shared.Next(12, 35);
            double x = x0 + (distance - 9) * ease + Random.Shared.NextDouble() * 2 - 1;
            double y = y0 + Random.Shared.NextDouble() * 3 - 1.5;
            sb.Append(",[")
              .Append(Math.Round(x, 1).ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Math.Round(y, 1).ToString(CultureInfo.InvariantCulture)).Append(",3,")
              .Append(t).Append(']');
        }
        t += Random.Shared.Next(18, 56);
        sb.Append(",[")
          .Append(Math.Round(x0 + distance - 9, 1).ToString(CultureInfo.InvariantCulture))
          .Append(',').Append(y0.ToString(CultureInfo.InvariantCulture)).Append(",2,")
          .Append(t).Append("]]");
        return sb.ToString();
    }

    // ---------------- 发短信 / 提交 ----------------

    public static async Task<RealNameResult> SendSmsAsync(string phone, string capTicket, string secCode)
    {
        var url = $"{Rnr}/securityCode?mobilePhone={Uri.EscapeDataString(phone)}" +
                  $"&capTicket={Uri.EscapeDataString(capTicket)}" +
                  $"&secCode={Uri.EscapeDataString(secCode)}&graphCode=&reason=1";
        var doc = await GetJsonAsync(url);
        int code = doc.GetProperty("code").GetInt32();
        var desc = doc.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        return new RealNameResult(code, desc);
    }

    public static async Task<RealNameResult> SubmitAsync(
        string token, string name, string id, string phone, string smsCode)
    {
        var body = JsonSerializer.Serialize(new
        {
            realName = name,
            residentId = id,
            mobilePhone = phone,
            securityCode = smsCode,
            capTicket = "",
            secCode = "",
        });
        var doc = await PostJsonAsync($"{Rnr}/register?token={Uri.EscapeDataString(token)}", body);
        int code = doc.GetProperty("code").GetInt32();
        var desc = doc.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        return new RealNameResult(code, desc);
    }

    // ---------------- 滑块图像处理 ----------------

    const int Cols = 20, Rows = 2, BgW = 260;

    /// <summary>
    /// 挑战图是 321x120：左边 260 是背景但被打散成 20 列 x 2 行的瓦片，
    /// 右边 61 是拼图块。loc 是置换表，第 n 格显示的是第 loc[n] 块。
    /// 还原之后用拼图块的轮廓当模板，在边缘图上找缺口。
    /// </summary>
    static List<(int X, double Score)> Candidates(byte[] png, int[] loc, int top)
    {
        var (w, h, bgra) = Decode(png);
        if (w != 321 || h != 120)
            throw new InvalidOperationException($"滑块图大小变了（{w}x{h}），得改算法");
        if (loc.Length != Cols * Rows)
            throw new InvalidOperationException($"loc 长度是 {loc.Length}，跟预期不符");

        int tw = BgW / Cols, th = h / Rows;

        // 1) 按 loc 把瓦片拼回去，同时转成灰度
        var gray = new double[BgW * h];
        for (int k = 0; k < Cols * Rows; k++)
        {
            int r = k / Cols, c = k % Cols;
            int s = loc[k], sr = s / Cols, sc = s % Cols;
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    int si = ((sr * th + y) * w + (sc * tw + x)) * 4;
                    gray[(r * th + y) * BgW + (c * tw + x)] =
                        0.299 * bgra[si + 2] + 0.587 * bgra[si + 1] + 0.114 * bgra[si];
                }
        }

        // 2) 拼图块的 alpha 就是它的轮廓
        int pw = w - BgW;
        var mask = new bool[pw * h];
        foreach (var thr in new[] { 100, 20 })
        {
            int onCount = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < pw; x++)
                {
                    bool on = bgra[(y * w + BgW + x) * 4 + 3] > thr;
                    mask[y * pw + x] = on;
                    if (on) onCount++;
                }
            if (onCount > 50) break;      // 这一档能框出块就不往下退
        }

        int minY = h, maxY = -1, minX = pw, maxX = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < pw; x++)
                if (mask[y * pw + x])
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
        if (maxY < 0) throw new InvalidOperationException("滑块图里找不到拼图块");

        var ry = new List<int>();
        var rx = new List<int>();
        for (int y = Math.Max(0, minY + 1); y <= Math.Min(h - 2, maxY - 1); y++)
            for (int x = Math.Max(0, minX + 1); x <= Math.Min(pw - 2, maxX - 1); x++)
            {
                if (!mask[y * pw + x]) continue;
                // 3x3 里只要有一个不在块里，这个点就算轮廓（等价于膨胀减腐蚀）
                bool isRing = false;
                for (int dy = -1; dy <= 1 && !isRing; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (!mask[(y + dy) * pw + (x + dx)]) { isRing = true; break; }
                if (!isRing) continue;
                ry.Add(y - minY);
                rx.Add(x - minX);
            }
        if (ry.Count == 0) throw new InvalidOperationException("拼图块轮廓是空的");

        int tw2 = rx.Max() + 1, th2 = ry.Max() + 1;

        // 3) 边缘图（PIL FIND_EDGES 那个 8 邻域核）
        var edge = new double[BgW * h];
        double max = 1e-9;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < BgW; x++)
            {
                double center = gray[y * BgW + x] * 8;
                double nb = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dy == 0 && dx == 0) continue;
                        int yy = Math.Clamp(y + dy, 0, h - 1);
                        int xx = Math.Clamp(x + dx, 0, BgW - 1);
                        nb += gray[yy * BgW + xx];
                    }
                double v = center - nb;
                if (v < 0) v = 0;
                edge[y * BgW + x] = v;
                if (v > max) max = v;
            }
        for (int i = 0; i < edge.Length; i++) edge[i] /= max;

        // 4) 模板匹配
        var hits = new List<(double Score, int X)>();
        for (int y = 0; y + th2 <= h; y++)
            for (int x = 0; x + tw2 <= BgW; x++)
            {
                double sum = 0;
                for (int i = 0; i < ry.Count; i++)
                    sum += edge[(y + ry[i]) * BgW + x + rx[i]];
                hits.Add((sum / ry.Count, x));
            }
        hits.Sort((a, b) => b.Score.CompareTo(a.Score));

        var outList = new List<(int, double)>();
        foreach (var (score, x) in hits)
        {
            if (outList.All(o => Math.Abs(o.Item1 - x) > 10)) outList.Add((x, score));
            if (outList.Count >= top) break;
        }
        return outList;
    }

    static (int W, int H, byte[] Bgra) Decode(byte[] png)
    {
        using var ms = new MemoryStream(png);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        var conv = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight;
        var data = new byte[w * h * 4];
        conv.CopyPixels(data, w * 4, 0);
        return (w, h, data);
    }

    // ---------------- 加解密 ----------------

    /// <summary>前端就是拿 capTicket 里几段拼出 16 字符当 AES 的 key 和 iv。</summary>
    static string DeriveKey(string cap) =>
        cap.Substring(1, 2) + cap.Substring(10, 3) + cap.Substring(20, 2) +
        cap.Substring(26, 5) + cap.Substring(21, 4);

    static string Enc(string plain, string key16)
    {
        var k = Encoding.UTF8.GetBytes(key16);
        using var aes = Aes.Create();
        aes.Key = k;
        aes.IV = k;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var enc = aes.CreateEncryptor();
        var data = Encoding.UTF8.GetBytes(plain);
        return Convert.ToBase64String(enc.TransformFinalBlock(data, 0, data.Length));
    }

    // ---------------- HTTP 小工具 ----------------

    static async Task<string> GetStringAsync(string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Referrer = new Uri(Rnr + "/");
        using var resp = await Http.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    static async Task<JsonElement> GetJsonAsync(string url)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync(url));
        return doc.RootElement.Clone();
    }

    static async Task<JsonElement> PostJsonAsync(string url, string json)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Referrer = new Uri(Rnr + "/");
        req.Headers.TryAddWithoutValidation("Origin", Rnr);
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    /// <summary>接口是 jsonp 返回的，剥掉外层回调。</summary>
    static JsonElement Unwrap(string text)
    {
        var s = text.Trim();
        int a = s.IndexOf('(');
        int b = s.LastIndexOf(')');
        if (a > 0 && b > a) s = s[(a + 1)..b];
        using var doc = JsonDocument.Parse(s);
        return doc.RootElement.Clone();
    }
}

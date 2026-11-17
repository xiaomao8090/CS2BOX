using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2Box.Services;

/// <summary>CS2 准星参数，可以和分享码 CSGO-xxxxx-xxxxx-xxxxx-xxxxx-xxxxx 互转。</summary>
public class Crosshair
{
    public int Style { get; set; } = 4;               // cl_crosshairstyle
    public double Size { get; set; } = 2.5;           // cl_crosshairsize
    public double Thickness { get; set; } = 1;        // cl_crosshairthickness
    public double Gap { get; set; } = -2;             // cl_crosshairgap
    public bool Outline { get; set; } = true;         // cl_crosshair_drawoutline
    public double OutlineThickness { get; set; } = 1; // cl_crosshair_outlinethickness
    public int Color { get; set; } = 1;               // cl_crosshaircolor，5 = 自定义 RGB
    public int R { get; set; } = 0;
    public int G { get; set; } = 255;
    public int B { get; set; } = 0;
    public bool UseAlpha { get; set; } = true;        // cl_crosshairusealpha
    public int Alpha { get; set; } = 255;             // cl_crosshairalpha
    public bool Dot { get; set; }                     // cl_crosshairdot
    public bool TStyle { get; set; }                  // cl_crosshair_t
    public bool GapUseWeaponValue { get; set; }       // cl_crosshairgap_useweaponvalue
    public bool FollowRecoil { get; set; }            // cl_crosshair_recoil
    public int SplitDistance { get; set; } = 3;       // cl_crosshair_dynamic_splitdist
    public double FixedGap { get; set; } = 3;         // cl_fixedcrosshairgap
    public double InnerSplitAlpha { get; set; }       // cl_crosshair_dynamic_splitalpha_innermod
    public double OuterSplitAlpha { get; set; } = 1;  // cl_crosshair_dynamic_splitalpha_outermod
    public double SplitSizeRatio { get; set; } = 1;   // cl_crosshair_dynamic_maxdist_splitratio

    public Crosshair Clone() => (Crosshair)MemberwiseClone();

    /// <summary>预设颜色 0~4 对应的 RGB；5 用自定义值。</summary>
    public (byte R, byte G, byte B) DisplayColor => Color switch
    {
        0 => (250, 50, 50),
        1 => (50, 250, 50),
        2 => (250, 250, 50),
        3 => (50, 50, 250),
        4 => (50, 250, 250),
        _ => ((byte)R, (byte)G, (byte)B),
    };

    // ---------- 控制台指令 ----------

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string B01(bool b) => b ? "1" : "0";

    public IEnumerable<string> Commands()
    {
        yield return $"cl_crosshairstyle {Style}";
        yield return $"cl_crosshairsize {F(Size)}";
        yield return $"cl_crosshairthickness {F(Thickness)}";
        yield return $"cl_crosshairgap {F(Gap)}";
        yield return $"cl_crosshair_drawoutline {B01(Outline)}";
        yield return $"cl_crosshair_outlinethickness {F(OutlineThickness)}";
        yield return $"cl_crosshaircolor {Color}";
        if (Color == 5)
        {
            yield return $"cl_crosshaircolor_r {R}";
            yield return $"cl_crosshaircolor_g {G}";
            yield return $"cl_crosshaircolor_b {B}";
        }
        yield return $"cl_crosshairusealpha {B01(UseAlpha)}";
        yield return $"cl_crosshairalpha {Alpha}";
        yield return $"cl_crosshairdot {B01(Dot)}";
        yield return $"cl_crosshair_t {B01(TStyle)}";
        yield return $"cl_crosshairgap_useweaponvalue {B01(GapUseWeaponValue)}";
        yield return $"cl_crosshair_recoil {B01(FollowRecoil)}";
    }

    public string CommandLine => string.Join("; ", Commands());

    // ---------- 分享码 ----------
    // 18 字节：[0]=校验和 [1]=版本 1，之后依次是各参数，整体按大端转成大整数后用 57 进制字典编码。

    const string Dict = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";
    static readonly Regex CodeRx = new(@"^CSGO(-[" + Dict + "]{5}){5}$");

    public static bool IsShareCode(string s) => CodeRx.IsMatch(s.Trim());

    public static Crosshair FromShareCode(string code)
    {
        code = code.Trim();
        if (!IsShareCode(code)) throw new FormatException("分享码格式不对，应该是 CSGO-xxxxx-xxxxx-xxxxx-xxxxx-xxxxx");

        var chars = code[5..].Replace("-", "");
        BigInteger n = 0;
        for (int i = chars.Length - 1; i >= 0; i--)
            n = n * 57 + Dict.IndexOf(chars[i]);

        var raw = n.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (raw.Length > 18) throw new FormatException("分享码校验失败，可能抄错了");
        var b = new byte[18];
        Array.Copy(raw, 0, b, 18 - raw.Length, raw.Length);

        int sum = 0;
        for (int i = 1; i < 18; i++) sum += b[i];
        if ((byte)sum != b[0]) throw new FormatException("分享码校验失败，可能抄错了");
        // 目前只认第 1 版布局；游戏后来出现了字段位置不同的新版本，按旧布局解析会得到错误数值
        if (b[1] != 1) throw new FormatException($"这是第 {b[1]} 版分享码，暂不支持，可以改用控制台指令");

        return new Crosshair
        {
            Gap = (sbyte)b[2] / 10.0,
            OutlineThickness = b[3] / 2.0,
            R = b[4], G = b[5], B = b[6],
            Alpha = b[7],
            SplitDistance = b[8] & 0x7F,
            FollowRecoil = (b[8] & 0x80) != 0,
            FixedGap = (sbyte)b[9] / 10.0,
            Color = b[10] & 7,
            Outline = (b[10] & 8) != 0,
            InnerSplitAlpha = (b[10] >> 4) / 10.0,
            OuterSplitAlpha = (b[11] & 0xF) / 10.0,
            SplitSizeRatio = (b[11] >> 4) / 10.0,
            Thickness = b[12] / 10.0,
            Style = (b[13] & 0xF) >> 1,
            Dot = (b[13] & 0x10) != 0,
            GapUseWeaponValue = (b[13] & 0x20) != 0,
            UseAlpha = (b[13] & 0x40) != 0,
            TStyle = (b[13] & 0x80) != 0,
            Size = (((b[15] & 0x1F) << 8) | b[14]) / 10.0,
        };
    }

    public string ToShareCode()
    {
        static byte Clamp(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
        static byte Signed(double v) => unchecked((byte)(sbyte)Math.Clamp((int)Math.Round(v * 10), -128, 127));

        int len = (int)Math.Round(Size * 10);
        var b = new byte[18];
        b[1] = 1;
        b[2] = Signed(Gap);
        b[3] = Clamp(OutlineThickness * 2);
        b[4] = Clamp(R); b[5] = Clamp(G); b[6] = Clamp(B);
        b[7] = Clamp(Alpha);
        b[8] = (byte)((SplitDistance & 0x7F) | (FollowRecoil ? 0x80 : 0));
        b[9] = Signed(FixedGap);
        b[10] = (byte)((Color & 7) | (Outline ? 8 : 0) | (Clamp(InnerSplitAlpha * 10) << 4));
        b[11] = (byte)((Clamp(OuterSplitAlpha * 10) & 0xF) | (Clamp(SplitSizeRatio * 10) << 4));
        b[12] = Clamp(Thickness * 10);
        b[13] = (byte)(((Style & 7) << 1) | (Dot ? 0x10 : 0) | (GapUseWeaponValue ? 0x20 : 0) | (UseAlpha ? 0x40 : 0) | (TStyle ? 0x80 : 0));
        b[14] = (byte)(len & 0xFF);
        b[15] = (byte)((len >> 8) & 0x1F);
        int sum = 0;
        for (int i = 1; i < 18; i++) sum += b[i];
        b[0] = (byte)sum;

        var n = new BigInteger(b, isUnsigned: true, isBigEndian: true);
        var sb = new StringBuilder();
        for (int i = 0; i < 25; i++)
        {
            sb.Append(Dict[(int)(n % 57)]);
            n /= 57;
        }
        var s = sb.ToString();
        return $"CSGO-{s[..5]}-{s[5..10]}-{s[10..15]}-{s[15..20]}-{s[20..25]}";
    }
}

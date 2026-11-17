using System.Text;

namespace CS2Box.Services;

/// <summary>VDF 里的一项：键、值（字符串或子节点），以及可选的条件标记，如 [$WIN32]。</summary>
public sealed class VdfEntry(string key, object value, string? condition = null)
{
    public string Key { get; } = key;
    public object Value { get; set; } = value;
    public string? Condition { get; set; } = condition;
}

/// <summary>
/// Valve KeyValues (.vdf) 节点。保留原始顺序、重复键和条件标记，写回时内容不丢。
/// （注释不会保留；Steam 生成的文件里没有注释。）
/// </summary>
public class VdfNode
{
    public List<VdfEntry> Items { get; } = [];

    VdfEntry? Find(string key) => Items.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public object? Get(string key) => Find(key)?.Value;
    public VdfNode? Node(string key) => Get(key) as VdfNode;
    public string? Str(string key) => Get(key) as string;

    public IEnumerable<(string Key, VdfNode Node)> Children =>
        Items.Where(e => e.Value is VdfNode).Select(e => (e.Key, (VdfNode)e.Value));

    /// <summary>设置值；键已存在就原地替换（保持顺序和条件标记），否则追加。</summary>
    public void Set(string key, object value)
    {
        if (Find(key) is { } e) e.Value = value;
        else Items.Add(new VdfEntry(key, value));
    }

    /// <summary>取子节点，没有就建一个。</summary>
    public VdfNode Ensure(string key)
    {
        if (Node(key) is { } n) return n;
        var created = new VdfNode();
        Items.Add(new VdfEntry(key, created));
        return created;
    }
}

public static class Vdf
{
    public static VdfNode ParseFile(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public static VdfNode Parse(string text)
    {
        int i = 0;
        return ReadNode(text, ref i);
    }

    /// <summary>先写临时文件再替换，避免写到一半出错把原文件弄坏。</summary>
    public static void WriteFile(string path, VdfNode root)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(root), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    public static string Serialize(VdfNode root)
    {
        var sb = new StringBuilder();
        Write(sb, root, 0);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, VdfNode node, int depth)
    {
        var ind = new string('\t', depth);
        foreach (var e in node.Items)
        {
            var cond = e.Condition is { } c ? " " + c : "";
            if (e.Value is VdfNode child)
            {
                sb.Append(ind).Append('"').Append(Escape(e.Key)).Append('"').Append(cond).Append('\n');
                sb.Append(ind).Append("{\n");
                Write(sb, child, depth + 1);
                sb.Append(ind).Append("}\n");
            }
            else
            {
                sb.Append(ind).Append('"').Append(Escape(e.Key)).Append("\"\t\t\"")
                  .Append(Escape((string)e.Value)).Append('"').Append(cond).Append('\n');
            }
        }
    }

    public static string Escape(string s) => s
        .Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r");

    static VdfNode ReadNode(string s, ref int i)
    {
        var node = new VdfNode();
        while (true)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return node;
            if (s[i] == '}') { i++; return node; }

            var key = ReadToken(s, ref i);
            var keyCond = ReadCondition(s, ref i);   // "key" [$X] { ... }
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '{')
            {
                i++;
                node.Items.Add(new VdfEntry(key, ReadNode(s, ref i), keyCond));
            }
            else
            {
                var value = ReadToken(s, ref i);
                var cond = ReadCondition(s, ref i);  // "key" "value" [$X]
                node.Items.Add(new VdfEntry(key, value, cond ?? keyCond));
            }
        }
    }

    /// <summary>读同一行里跟在后面的条件标记，例如 [$WIN32]、[!$OSX]；没有就返回 null。</summary>
    static string? ReadCondition(string s, ref int i)
    {
        int j = i;
        while (j < s.Length && (s[j] == ' ' || s[j] == '\t')) j++;
        if (j >= s.Length || s[j] != '[') return null;
        int start = j;
        while (j < s.Length && s[j] != ']' && s[j] != '\n') j++;
        if (j >= s.Length || s[j] != ']') return null;  // 没闭合，不当作条件
        i = j + 1;
        return s[start..i];
    }

    static string ReadToken(string s, ref int i)
    {
        var sb = new StringBuilder();
        if (i < s.Length && s[i] == '"')
        {
            i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        default: sb.Append('\\').Append(s[i]); break; // 不认识的转义原样保留
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
        }
        else
        {
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '{' && s[i] != '}' && s[i] != '"') sb.Append(s[i++]);
        }
        return sb.ToString();
    }

    static void SkipWs(string s, ref int i)
    {
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) i++;
            else if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/')
                while (i < s.Length && s[i] != '\n') i++;
            else break;
        }
    }
}

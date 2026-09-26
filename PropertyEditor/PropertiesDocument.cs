namespace PropertyEditor;

public enum PropertyRowKind
{
    Blank,
    Comment,
    Entry
}

public enum PropertySaveMode
{
    Java,
    Utf8
}

public sealed class PropertyRow
{
    public PropertyRowKind Kind { get; init; }
    public string Raw { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? RawValue { get; set; }
    public string Separator { get; set; } = "=";
}

public sealed class PropertiesDocument
{
    public List<PropertyRow> Rows { get; } = [];
    public string Newline { get; set; } = "\r\n";
    public bool EndsWithNewline { get; set; } = true;
    public PropertySaveMode Mode { get; set; } = PropertySaveMode.Java;

    public static PropertiesDocument Load(byte[] bytes)
    {
        var document = new PropertiesDocument
        {
            Newline = bytes.Contains((byte)13) ? "\r\n" : "\n",
            Mode = DetectMode(bytes)
        };
        var text = Decode(bytes, document.Mode);
        document.EndsWithNewline = text.EndsWith('\n');
        document.Rows.AddRange(Parse(text));
        return document;
    }

    public byte[] Save()
    {
        var text = Serialize(Rows, Mode, Newline, EndsWithNewline || Rows.Count > 0);
        return Encode(text, Mode);
    }

    public static PropertySaveMode DetectMode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return PropertySaveMode.Utf8;

        string utf8;
        try
        {
            utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            return PropertySaveMode.Java;
        }

        var latin1 = System.Text.Encoding.Latin1.GetString(bytes);
        var hasEscape = System.Text.RegularExpressions.Regex.IsMatch(latin1, @"\\u[0-9a-fA-F]{4}");
        var hasNonAscii = utf8.Any(ch => ch > '\u007f');
        if (hasNonAscii && !hasEscape)
            return PropertySaveMode.Utf8;
        return PropertySaveMode.Java;
    }

    public static IEnumerable<PropertyRow> Parse(string text)
    {
        foreach (var line in JoinLogicalLines(text))
            yield return ParseLogicalLine(line);
    }

    public static string Serialize(IReadOnlyList<PropertyRow> rows, PropertySaveMode mode, string newline, bool trailingNewline)
    {
        var escapeUnicode = mode == PropertySaveMode.Java;
        var lines = rows.Select(row => row.Kind switch
        {
            PropertyRowKind.Blank => "",
            PropertyRowKind.Comment => row.Raw,
            _ => Escape(row.Key, escapeSpaceAlways: true, escapeUnicode) +
                 (string.IsNullOrEmpty(row.Separator) ? "=" : row.Separator) +
                 Escape(row.Value, escapeSpaceAlways: false, escapeUnicode)
        });
        var body = string.Join(newline, lines);
        return trailingNewline && rows.Count > 0 ? body + newline : body;
    }

    public static string Unescape(string source)
    {
        var output = new System.Text.StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '\\')
            {
                output.Append(source[i]);
                continue;
            }

            i++;
            if (i >= source.Length)
            {
                output.Append('\\');
                break;
            }

            switch (source[i])
            {
                case 't': output.Append('\t'); break;
                case 'n': output.Append('\n'); break;
                case 'r': output.Append('\r'); break;
                case 'f': output.Append('\f'); break;
                case 'u':
                    if (i + 4 < source.Length && IsHex4(source, i + 1))
                    {
                        output.Append((char)Convert.ToInt32(source.Substring(i + 1, 4), 16));
                        i += 4;
                    }
                    else
                    {
                        output.Append('u');
                    }
                    break;
                default:
                    output.Append(source[i]);
                    break;
            }
        }
        return output.ToString();
    }

    public static string Escape(string source, bool escapeSpaceAlways, bool escapeUnicode)
    {
        var output = new System.Text.StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var code = source[i];
            if (code > 61 && code < 127)
            {
                if (code == '\\') output.Append("\\\\");
                else output.Append(code);
                continue;
            }

            switch (code)
            {
                case ' ':
                    if (i == 0 || escapeSpaceAlways) output.Append('\\');
                    output.Append(' ');
                    break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\f': output.Append("\\f"); break;
                case '=':
                case ':':
                case '#':
                case '!':
                    output.Append('\\');
                    output.Append(code);
                    break;
                default:
                    if (escapeUnicode && (code < 0x20 || code > 0x7e))
                        output.Append("\\u").Append(((int)code).ToString("X4"));
                    else
                        output.Append(code);
                    break;
            }
        }
        return output.ToString();
    }

    private static string Decode(byte[] bytes, PropertySaveMode mode)
    {
        if (mode == PropertySaveMode.Utf8)
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
        return System.Text.Encoding.Latin1.GetString(bytes);
    }

    private static byte[] Encode(string text, PropertySaveMode mode)
    {
        if (mode == PropertySaveMode.Utf8)
            return System.Text.Encoding.UTF8.GetBytes(text);

        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
            bytes[i] = (byte)text[i];
        return bytes;
    }

    private static List<string> JoinLogicalLines(string text)
    {
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        if (text.EndsWith('\n') && lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        var logical = new List<string>();
        var current = "";
        var joining = false;
        foreach (var original in lines)
        {
            var line = joining ? original.TrimStart(' ', '\t', '\f') : original;
            if (Continues(line))
            {
                current += line[..^1];
                joining = true;
            }
            else
            {
                logical.Add(current + line);
                current = "";
                joining = false;
            }
        }
        if (joining)
            logical.Add(current);
        return logical;
    }

    private static bool Continues(string line)
    {
        var count = 0;
        for (var i = line.Length - 1; i >= 0 && line[i] == '\\'; i--)
            count++;
        return count % 2 == 1;
    }

    private static PropertyRow ParseLogicalLine(string line)
    {
        var i = 0;
        while (i < line.Length && IsWhitespace(line[i])) i++;
        if (i >= line.Length)
            return new PropertyRow { Kind = PropertyRowKind.Blank };
        if (line[i] is '#' or '!')
            return new PropertyRow { Kind = PropertyRowKind.Comment, Raw = line };

        var rawKey = new System.Text.StringBuilder();
        while (i < line.Length)
        {
            var ch = line[i];
            if (ch == '\\')
            {
                rawKey.Append(ch);
                if (i + 1 < line.Length) rawKey.Append(line[++i]);
                i++;
                continue;
            }
            if (ch is '=' or ':' || IsWhitespace(ch)) break;
            rawKey.Append(ch);
            i++;
        }

        var separator = "";
        if (i < line.Length && IsWhitespace(line[i]))
        {
            while (i < line.Length && IsWhitespace(line[i])) separator += line[i++];
            if (i < line.Length && line[i] is '=' or ':')
            {
                separator += line[i++];
                while (i < line.Length && IsWhitespace(line[i])) separator += line[i++];
            }
        }
        else if (i < line.Length && line[i] is '=' or ':')
        {
            separator += line[i++];
            while (i < line.Length && IsWhitespace(line[i])) separator += line[i++];
        }
        if (separator.Length == 0) separator = "=";

        return new PropertyRow
        {
            Kind = PropertyRowKind.Entry,
            Key = Unescape(rawKey.ToString()),
            Value = Unescape(line[i..]),
            RawValue = line[i..],
            Separator = separator
        };
    }

    public static string DecodeUnicodeEscapes(string text)
    {
        var output = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '\\')
            {
                output.Append("\\\\");
                i++;
                continue;
            }
            if (text[i] == '\\' && i + 5 < text.Length && text[i + 1] == 'u' && IsHex4(text, i + 2))
            {
                output.Append((char)Convert.ToInt32(text.Substring(i + 2, 4), 16));
                i += 5;
                continue;
            }
            output.Append(text[i]);
        }
        return output.ToString();
    }

    public static string EncodeNonAscii(string text)
    {
        var output = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is '\r' or '\n' || (ch >= 0x20 && ch <= 0x7e))
                output.Append(ch);
            else
                output.Append("\\u").Append(((int)ch).ToString("X4"));
        }
        return output.ToString();
    }

    private static bool IsWhitespace(char ch) => ch is ' ' or '\t' or '\f';

    private static bool IsHex4(string source, int index)
    {
        for (var i = 0; i < 4; i++)
        {
            var ch = source[index + i];
            var hex = ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hex) return false;
        }
        return true;
    }
}

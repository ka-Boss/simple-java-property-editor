using System.Text;

namespace PropertyEditor;

enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    ShiftJis
}

static class TextCodec
{
    static TextCodec()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Name(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8Bom => "UTF-8（BOMつき）",
        TextEncodingKind.ShiftJis => "Shift_JIS",
        _ => "UTF-8"
    };

    public static string Decode(byte[] bytes, TextEncodingKind kind)
    {
        var text = EncodingOf(kind).GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];
        return PropertiesDocument.DecodeUnicodeEscapes(text);
    }

    public static byte[] Encode(string text, TextEncodingKind kind) =>
        EncodingOf(kind).GetBytes(text);

    private static Encoding EncodingOf(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        TextEncodingKind.ShiftJis => Encoding.GetEncoding(932),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };
}

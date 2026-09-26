using PropertyEditor;
using Xunit;

namespace PropertyEditor.Tests;

public class PropertiesDocumentTests
{
    [Fact]
    public void Japanese_escape_roundtrips()
    {
        var source = "# comment \\u3053\\u308c\r\napp.name=\\u8acb\\u6c42\\u66f8\r\nmessage=line1\\\r\n  line2\r\nempty=\r\n";
        var bytes = System.Text.Encoding.Latin1.GetBytes(source);
        var document = PropertiesDocument.Load(bytes);

        Assert.Equal(PropertySaveMode.Java, document.Mode);
        Assert.Equal("請求書", document.Rows[1].Value);
        Assert.Equal("line1line2", document.Rows[2].Value);

        var saved = System.Text.Encoding.Latin1.GetString(document.Save());
        var again = PropertiesDocument.Load(System.Text.Encoding.Latin1.GetBytes(saved));
        Assert.Equal("請求書", again.Rows[1].Value);
        Assert.Equal("# comment \\u3053\\u308c", again.Rows[0].Raw);
        Assert.Contains("\\u8ACB\\u6C42\\u66F8", saved);
    }

    [Fact]
    public void Utf8_file_stays_utf8()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("title=請求書\n");
        var document = PropertiesDocument.Load(bytes);
        Assert.Equal(PropertySaveMode.Utf8, document.Mode);
        Assert.Equal("請求書", document.Rows[0].Value);
        document.Rows[0].Value = "合計";
        var saved = System.Text.Encoding.UTF8.GetString(document.Save());
        Assert.Equal("title=合計\n", saved);
    }

    [Fact]
    public void File_shape_decodes_unicode_only()
    {
        var source = "# \\u30b5\\u30f3\\u30d7\\u30eb\r\napp.name=\\u30d7\\u30ed\\u30d1\\u30c6\\u30a3\r\npath=C:\\\\temp\\\\sample\r\nnote=1\\\r\n  2\r\n";
        var decoded = PropertiesDocument.DecodeUnicodeEscapes(source);
        Assert.Equal("# サンプル\r\napp.name=プロパティ\r\npath=C:\\\\temp\\\\sample\r\nnote=1\\\r\n  2\r\n", decoded);
        Assert.Equal(source.Replace("\\u30b5\\u30f3\\u30d7\\u30eb", "\\u30B5\\u30F3\\u30D7\\u30EB").Replace("\\u30d7\\u30ed\\u30d1\\u30c6\\u30a3", "\\u30D7\\u30ED\\u30D1\\u30C6\\u30A3"), PropertiesDocument.EncodeNonAscii(decoded));
    }
}

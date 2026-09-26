namespace PropertyEditor;

static class AppSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Simple Java Property Editor",
        "encoding.txt");

    public static TextEncodingKind Load()
    {
        try
        {
            return File.ReadAllText(FilePath).Trim() switch
            {
                "utf8-bom" => TextEncodingKind.Utf8Bom,
                "shift_jis" => TextEncodingKind.ShiftJis,
                _ => TextEncodingKind.Utf8
            };
        }
        catch (IOException)
        {
            return TextEncodingKind.Utf8;
        }
        catch (UnauthorizedAccessException)
        {
            return TextEncodingKind.Utf8;
        }
    }

    public static void Save(TextEncodingKind kind)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var value = kind switch
        {
            TextEncodingKind.Utf8Bom => "utf8-bom",
            TextEncodingKind.ShiftJis => "shift_jis",
            _ => "utf8"
        };
        File.WriteAllText(FilePath, value);
    }
}

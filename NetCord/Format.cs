using System.Text.RegularExpressions;

namespace NetCord;

public static partial class Format
{
    public static string Bold(ReadOnlySpan<char> text) => $"**{text.ToString()}**";
    public static string Italic(ReadOnlySpan<char> text) => $"*{text.ToString()}*";
    public static string StrikeOut(ReadOnlySpan<char> text) => $"~~{text.ToString()}~~";
    public static string Underline(ReadOnlySpan<char> text) => $"__{text.ToString()}__";
    public static string Spoiler(ReadOnlySpan<char> text) => $"||{text.ToString()}||";
    public static string EscapeUrl(ReadOnlySpan<char> url) => $"<{url.ToString()}>";
    public static string Link(ReadOnlySpan<char> text, ReadOnlySpan<char> url) => $"[{text.ToString()}]({url.ToString()})";
    public static string SmallCodeBlock(ReadOnlySpan<char> code) => $"`{code.ToString()}`";
    public static string CodeBlock(ReadOnlySpan<char> code, ReadOnlySpan<char> formatter = default) => $"```{formatter.ToString()}\n{code.ToString()}```";
    public static string Timestamp(DateTimeOffset dateTime, TimestampStyle? style) => new Timestamp(dateTime, style).ToString();
    public static string Quote(ReadOnlySpan<char> text) => $">>> {text.ToString()}";
    public static string Escape(string text) => EscapeRegex().Replace(text, @"\$0");

    private static Regex EscapeRegex() => _escapeRegex;
    
    private static readonly Regex _escapeRegex = new(@"[\*_~`.:/>|]", RegexOptions.Compiled);
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CyberWall.Common.I18n;

namespace CyberWall.UI.Services;

internal static class ReleaseNotes
{
    internal static string[] Peek(string? body, Lang language)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];

        const RegexOptions html = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        const string details = @"<details\b[^>]*>\s*<summary\b[^>]*>(?<title>.*?)</summary>(?<notes>.*?)</details>";
        var spanish = language == Lang.Es
            ? Regex.Matches(body, details, html).FirstOrDefault(block =>
                Regex.IsMatch(block.Groups["title"].Value, "español|spanish", RegexOptions.IgnoreCase))
            : null;
        var content = spanish?.Groups["notes"].Value ?? Regex.Replace(body, details, "", html);
        content = Regex.Replace(content, @"<!--.*?-->|<p\b[^>]*>.*?</p>|```.*?```", "", html);
        content = Regex.Replace(content,
            @"^#{2,6}[^\S\r\n]+[^\r\n]*?(?:Before you install|Antes de instalar|Downloads|Descargas|VirusTotal|Checksums)[^\r\n]*(?:\r?\n|\z).*?(?=^#{1,6}[^\S\r\n]|\z)",
            "", html | RegexOptions.Multiline);

        var topLevel = new List<string>();
        var nested = new List<string>();
        var paragraphs = new List<string>();
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or '|' or '>' or '<' ||
                line.StartsWith("---", StringComparison.Ordinal) ||
                line.Contains("Full changelog:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Crafted with precision", StringComparison.OrdinalIgnoreCase))
                continue;

            bool bullet = line.Length > 2 && line[1] == ' ' && line[0] is '-' or '*' or '+' or '•';
            var text = Clean(bullet ? line[2..] : line);
            if (text.Length == 0) continue;
            if (!bullet) paragraphs.Add(text);
            else if (raw.Length - raw.TrimStart().Length <= 1) topLevel.Add(text);
            else nested.Add(text);
        }
        return (topLevel.Count > 0 ? topLevel : nested.Count > 0 ? nested : paragraphs).Distinct().Take(4).ToArray();
    }

    private static string Clean(string line)
    {
        line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]*\)", "$1");
        line = line.Replace("**", "").Replace("__", "").Replace("`", "").Trim();
        int prefix = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && Rune.GetUnicodeCategory(rune) is not
                (UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.NonSpacingMark or UnicodeCategory.Format))
                break;
            prefix += rune.Utf16SequenceLength;
        }
        return line[prefix..].TrimEnd(':').TrimEnd();
    }
}

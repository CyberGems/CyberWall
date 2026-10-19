using System.Reflection;
using System.Text.RegularExpressions;
using CyberWall.Common.I18n;

namespace CyberWall.Tests;

/// <summary>
/// Guards the translation catalog. A key used in the code but missing from the map
/// renders the raw key in the user interface, which is easy to introduce and hard to
/// notice, so the whole source tree is checked here.
/// </summary>
public class StringsTests
{
    private static readonly Regex CallPattern = new(@"Strings\.T\(\s*""([A-Za-z0-9_]+)""", RegexOptions.Compiled);

    public static TheoryData<string> AllKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in KnownKeys()) data.Add(key);
        return data;
    }

    [Fact]
    public void EveryKeyUsedInTheCodeExistsInTheCatalog()
    {
        var known = KnownKeys().ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in CallPattern.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (!known.Contains(key)) missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryCatalogKeyIsUsedSomewhere()
    {
        var source = string.Join("\n", SourceFiles().Select(File.ReadAllText));
        var unused = KnownKeys().Where(key => !source.Contains($"\"{key}\"", StringComparison.Ordinal)).ToList();

        Assert.Empty(unused);
    }

    [Fact]
    public void EveryKeyHasAnEnglishAndASpanishTranslation()
    {
        var missing = new List<string>();
        foreach (var key in KnownKeys())
        {
            var value = Strings.T(key);
            if (string.IsNullOrWhiteSpace(value) || value == key)
                missing.Add(key);
        }
        Assert.Empty(missing);
    }

    [Fact]
    public void TranslationsDifferBetweenLanguages()
    {
        // Terms that are written the same in both languages.
        var intentionallyIdentical = new HashSet<string>(StringComparer.Ordinal) { "NavFirewall", "MonitorN" };

        var previous = Strings.Current;
        try
        {
            foreach (var key in KnownKeys())
            {
                if (intentionallyIdentical.Contains(key)) continue;

                Strings.Current = CyberWall.Common.I18n.Lang.En;
                var english = Strings.T(key);
                Strings.Current = CyberWall.Common.I18n.Lang.Es;
                var spanish = Strings.T(key);

                // Identical values are only acceptable for values made of symbols.
                var meaningful = english.Any(c => char.IsLetter(c));
                if (meaningful)
                    Assert.True(english != spanish, $"'{key}' has the same text in both languages.");
            }
        }
        finally
        {
            Strings.Current = previous;
        }
    }

    [Fact]
    public void UnknownKeysReturnTheKeyInsteadOfThrowing()
    {
        Assert.Equal("ThisKeyDoesNotExist", Strings.T("ThisKeyDoesNotExist"));
    }

    private static IEnumerable<string> KnownKeys()
    {
        var field = typeof(Strings).GetField("Map", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var map = Assert.IsAssignableFrom<System.Collections.IDictionary>(field!.GetValue(null));
        return map.Keys.Cast<string>();
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = RepositoryRoot();
        return Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("I18n" + Path.DirectorySeparatorChar + "Strings.cs", StringComparison.OrdinalIgnoreCase));
    }

    internal static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CyberWall.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }
}

namespace CyberWall.UI;

internal sealed record SuiteApp(string Slug, string Name, string PitchKey, string Site);

internal static class SuiteCatalog
{
    public static IReadOnlyList<SuiteApp> Others { get; } =
    [
        new("cyberclock", "CyberClock", "Suite_cyberclock", "https://cybergems.org/apps/cyberclock/"),
        new("cyberfeeds", "CyberFeeds", "Suite_cyberfeeds", "https://cybergems.org/apps/cyberfeeds/"),
        new("cyberlauncher", "CyberLauncher", "Suite_cyberlauncher", "https://cybergems.org/apps/cyberlauncher/"),
        new("cybermanager", "CyberManager", "Suite_cybermanager", "https://cybergems.org/apps/cybermanager/"),
        new("cybernotes", "CyberNotes", "Suite_cybernotes", "https://cybergems.org/apps/cybernotes/"),
        new("cyberpaste", "CyberPaste", "Suite_cyberpaste", "https://cybergems.org/apps/cyberpaste/"),
        new("cybersnap", "CyberSnap", "Suite_cybersnap", "https://cybergems.org/apps/cybersnap/"),
        new("cybertray", "CyberTray", "Suite_cybertray", "https://cybergems.org/apps/cybertray/"),
        new("cyberviewer", "CyberViewer", "Suite_cyberviewer", "https://cybergems.org/apps/cyberviewer/"),
    ];

    public static IReadOnlyList<SuiteApp> Pick(int count)
    {
        var pool = Others.ToArray();
        Random.Shared.Shuffle(pool);
        return pool.Take(count).ToArray();
    }
}

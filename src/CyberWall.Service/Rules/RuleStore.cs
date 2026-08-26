using System.Text.Json;
using CyberWall.Common;
using CyberWall.Common.Models;

namespace CyberWall.Service.Rules;

public sealed class RuleStore
{
    private readonly string _path;
    private readonly Dictionary<string, AppRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public RuleStore(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "rules.json");
        Load();
    }

    public IReadOnlyCollection<AppRule> All { get { lock (_lock) return _rules.Values.ToList(); } }

    public bool TryGet(string appPath, out AppRule rule)
    {
        lock (_lock)
        {
            try
            {
                if (_rules.TryGetValue(AppRule.Normalize(appPath), out rule!)) return true;
            }
            catch { }

            if (!PackagePath.TryGetFamilyName(appPath, out var pfn))
            {
                rule = null!;
                return false;
            }

            foreach (var r in _rules.Values)
            {
                var rulePfn = r.PackageFamilyName;
                if (string.IsNullOrEmpty(rulePfn))
                    PackagePath.TryGetFamilyName(r.AppPath, out rulePfn);
                if (!string.IsNullOrEmpty(rulePfn) && pfn.Equals(rulePfn, StringComparison.OrdinalIgnoreCase))
                {
                    rule = r;
                    return true;
                }
            }

            if (TryGetVersionedWebViewRule(appPath, out rule))
                return true;

            rule = null!;
            return false;
        }
    }

    public void Upsert(AppRule rule)
    {
        lock (_lock) _rules[AppRule.Normalize(rule.AppPath)] = rule;
        Save();
    }

    public void Remove(string appPath)
    {
        lock (_lock) _rules.Remove(AppRule.Normalize(appPath));
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<AppRule>>(json);
            if (list == null) return;
            lock (_lock) foreach (var r in list) _rules[AppRule.Normalize(r.AppPath)] = r;
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            List<AppRule> snap;
            lock (_lock) snap = _rules.Values.ToList();
            var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch { }
    }

    private bool TryGetVersionedWebViewRule(string appPath, out AppRule rule)
    {
        rule = null!;
        var stablePath = StableWebViewPath(appPath);
        if (stablePath == null) return false;

        foreach (var candidate in _rules.Values)
        {
            if (StableWebViewPath(candidate.AppPath) == stablePath)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    private static string? StableWebViewPath(string path)
    {
        try
        {
            var fileName = Path.GetFileName(path);
            if (!fileName.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase))
                return null;

            var versionDir = Directory.GetParent(path);
            var applicationDir = versionDir?.Parent;
            if (applicationDir == null ||
                !applicationDir.Name.Equals("Application", StringComparison.OrdinalIgnoreCase))
                return null;

            return AppRule.Normalize(Path.Combine(applicationDir.FullName, fileName));
        }
        catch
        {
            return null;
        }
    }
}

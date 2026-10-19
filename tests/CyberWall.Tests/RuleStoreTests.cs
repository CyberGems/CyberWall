using CyberWall.Common.Models;
using CyberWall.Service.Rules;

namespace CyberWall.Tests;

public class RuleStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public RuleStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "CyberWallTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "rules.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AnUnknownPathHasNoRule()
    {
        var store = new RuleStore(_path);
        Assert.False(store.TryGet(@"C:\Apps\Unknown.exe", out _));
    }

    [Fact]
    public void ARuleIsFoundByItsExactPath()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule { AppPath = @"C:\Apps\Demo.exe", Verdict = Verdict.Allow });

        Assert.True(store.TryGet(@"C:\Apps\Demo.exe", out var rule));
        Assert.Equal(Verdict.Allow, rule.Verdict);
    }

    [Fact]
    public void ThePathIsMatchedCaseInsensitively()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule { AppPath = @"C:\Apps\Demo.exe", Verdict = Verdict.Allow });

        Assert.True(store.TryGet(@"C:\APPS\demo.EXE", out var rule));
        Assert.Equal(Verdict.Allow, rule.Verdict);
    }

    [Fact]
    public void AStoreAppIsFoundByItsPackageFamilyName()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule
        {
            AppPath = @"C:\Program Files\WindowsApps\Demo_1.0_x64__publisher\Demo.exe",
            Verdict = Verdict.Allow,
            PackageFamilyName = "Demo_publisher",
        });

        // A different version of the same package resolves to the same rule.
        Assert.True(store.TryGet(@"C:\Program Files\WindowsApps\Demo_2.0_x64__publisher\Demo.exe", out var rule));
        Assert.Equal(Verdict.Allow, rule.Verdict);
    }

    [Fact]
    public void AWebView2RuleSurvivesARuntimeUpdate()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule
        {
            AppPath = @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application\120.0.1.2\msedgewebview2.exe",
            Verdict = Verdict.Allow,
        });

        Assert.True(store.TryGet(@"C:\Program Files (x86)\Microsoft\EdgeWebView\Application\121.0.2.3\msedgewebview2.exe", out var rule));
        Assert.Equal(Verdict.Allow, rule.Verdict);
    }

    [Fact]
    public void ADefenderRuleSurvivesAPlatformUpdate()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule
        {
            AppPath = @"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.24010.2-0\MsMpEng.exe",
            Verdict = Verdict.Allow,
        });

        Assert.True(store.TryGet(@"C:\ProgramData\Microsoft\Windows Defender\Platform\4.18.24011.3-0\MsMpEng.exe", out var rule));
        Assert.Equal(Verdict.Allow, rule.Verdict);
    }

    [Fact]
    public void RulesArePersistedAndReloaded()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule { AppPath = @"C:\Apps\Persisted.exe", Verdict = Verdict.Block });

        var reloaded = new RuleStore(_path);
        Assert.True(reloaded.TryGet(@"C:\Apps\Persisted.exe", out var rule));
        Assert.Equal(Verdict.Block, rule.Verdict);
    }

    [Fact]
    public void RemovedRulesAreNoLongerFound()
    {
        var store = new RuleStore(_path);
        store.Upsert(new AppRule { AppPath = @"C:\Apps\Gone.exe", Verdict = Verdict.Block });
        store.Remove(@"C:\Apps\Gone.exe");

        Assert.False(store.TryGet(@"C:\Apps\Gone.exe", out _));
    }

    [Fact]
    public void ReloadIfChangedPicksUpAFileWrittenByAnotherHost()
    {
        var store = new RuleStore(_path);
        Assert.False(store.ReloadIfChanged());

        File.WriteAllText(_path, "[{\"AppPath\":\"C:\\\\Apps\\\\Other.exe\",\"Verdict\":1}]");
        File.SetLastWriteTimeUtc(_path, DateTime.UtcNow.AddSeconds(1));

        Assert.True(store.ReloadIfChanged());
        Assert.True(store.TryGet(@"C:\Apps\Other.exe", out var rule));
        Assert.Equal(Verdict.Block, rule.Verdict);
    }
}

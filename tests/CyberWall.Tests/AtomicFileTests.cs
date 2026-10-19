using CyberWall.Common.IO;

namespace CyberWall.Tests;

public class AtomicFileTests : IDisposable
{
    private readonly string _dir;

    public AtomicFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "CyberWallTests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheContentIsWrittenAndReadBack()
    {
        var path = Path.Combine(_dir, "state.json");
        AtomicFile.WriteAllText(path, "{ \"value\": 1 }");

        Assert.Equal("{ \"value\": 1 }", File.ReadAllText(path));
    }

    [Fact]
    public void TheDirectoryIsCreatedWhenItDoesNotExist()
    {
        var path = Path.Combine(_dir, "nested", "deeper", "state.json");
        AtomicFile.WriteAllText(path, "content");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void NoTemporaryFileIsLeftBehind()
    {
        var path = Path.Combine(_dir, "state.json");
        AtomicFile.WriteAllText(path, "content");

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void AnExistingFileIsReplacedCompletely()
    {
        var path = Path.Combine(_dir, "state.json");
        AtomicFile.WriteAllText(path, "a long previous content");
        AtomicFile.WriteAllText(path, "short");

        Assert.Equal("short", File.ReadAllText(path));
    }

    [Fact]
    public void BinaryContentIsWrittenAndReadBack()
    {
        var path = Path.Combine(_dir, "data.bin");
        var payload = new byte[] { 1, 2, 3, 250 };
        AtomicFile.WriteAllBytes(path, payload);

        Assert.Equal(payload, File.ReadAllBytes(path));
    }
}

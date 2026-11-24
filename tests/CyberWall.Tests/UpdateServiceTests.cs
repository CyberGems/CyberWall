using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private const string ReleaseRoot = "https://github.com/CyberGems/CyberWall/releases";
    private readonly string _directory = Directory.CreateTempSubdirectory("CyberWall-update-test-").FullName;
    private string Destination => Path.Combine(_directory, "CyberWall-Setup-test.exe");

    [Fact]
    public void ParseRelease_PrefersInstallerAndUsesItsOwnPublishedHash()
    {
        var installer = "CyberWall-Setup-v2.0.0.exe";
        var portable = $"CyberWall-v2.0.0-Portable-{UpdateService.GetRuntimeChannel()}.zip";
        var installerHash = new string('b', 64);
        var portableHash = new string('c', 64);
        var notes = $"### Highlights\n- New release\n**Portable**: https://www.virustotal.com/gui/file/{portableHash}\n" +
                    $"**Installer**: https://www.virustotal.com/gui/file/{installerHash}";
        var result = UpdateService.ParseRelease(ReleaseJson(notes, [
            Asset(portable, $"sha256:{portableHash}"), Asset(installer)
        ]), new Version(1, 0, 0, 0));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(notes, result.ReleaseNotes);
        Assert.Equal(installer, result.AssetName);
        Assert.Equal(installerHash, result.AssetSha256);
        Assert.Null(result.PackageError);
    }

    [Fact]
    public void ParseRelease_DoesNotUseSourceArchiveOrTreatEquivalentVersionsAsAnUpdate()
    {
        var json = ReleaseJson(null, [Asset("CyberWall-Setup-v2.0.0-win-invalid.exe")]);
        var update = UpdateService.ParseRelease(json, new Version(1, 0));
        var installed = UpdateService.ParseRelease(json, new Version(2, 0, 0, 0));

        Assert.True(update.IsUpdateAvailable);
        Assert.Null(update.DownloadUrl);
        Assert.Equal("UpdateNoPackage", update.PackageError);
        Assert.False(installed.IsUpdateAvailable);
        Assert.False(installed.IsCheckError);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Download_PublishesOnlyVerifiedFiles(bool knownSize)
    {
        byte[] bytes = [10, 20, 30, 40];
        using var content = new StreamContent(new NonSeekableStream(bytes));
        if (knownSize) content.Headers.ContentLength = bytes.Length;
        using var http = new HttpClient(new StaticResponse(content));
        var progress = new ProgressRecorder();
        await UpdateService.DownloadAssetAsync(http, new Uri(ReleaseRoot), Destination,
            Convert.ToHexString(SHA256.HashData(bytes)), progress);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(Destination));
        Assert.Equal(new UpdateDownloadProgress(bytes.Length, knownSize ? bytes.Length : null), progress.Values.Last());
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    [Fact]
    public async Task Download_BadChecksumKeepsPreviousFileAndDiscardsPartial()
    {
        byte[] previous = [44, 55];
        await File.WriteAllBytesAsync(Destination, previous);
        using var http = new HttpClient(new StaticResponse(new ByteArrayContent([1, 2, 3])));

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.DownloadAssetAsync(
            http, new Uri(ReleaseRoot), Destination, new string('0', 64), new ProgressRecorder()));

        Assert.Equal(previous, await File.ReadAllBytesAsync(Destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    [Fact]
    public async Task Download_CancelsDuringTransferAndDiscardsPartial()
    {
        using var cancel = new CancellationTokenSource();
        using var http = new HttpClient(new StaticResponse(new StreamContent(new CancellableStream(cancel))));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateService.DownloadAssetAsync(
            http, new Uri(ReleaseRoot), Destination, new string('a', 64), new ProgressRecorder(), cancel.Token));

        Assert.False(File.Exists(Destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    private static object Asset(string name, string? digest = null) =>
        new { name, browser_download_url = $"{ReleaseRoot}/download/v2.0.0/{name}", digest };

    private static string ReleaseJson(string? body, object[] assets) => JsonSerializer.Serialize(new
    {
        tag_name = "v2.0.0",
        html_url = $"{ReleaseRoot}/tag/v2.0.0",
        zipball_url = "https://api.github.com/repos/CyberGems/CyberWall/zipball/v2.0.0",
        body,
        assets
    });

    private sealed class StaticResponse(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class ProgressRecorder : IProgress<UpdateDownloadProgress>
    {
        public List<UpdateDownloadProgress> Values { get; } = [];
        public void Report(UpdateDownloadProgress value) => Values.Add(value);
    }

    private class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class CancellableStream(CancellationTokenSource cancel) : NonSeekableStream([1, 2, 3])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancel.Cancel();
            return ValueTask.FromCanceled<int>(cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(_directory))
            File.Delete(path);
        Directory.Delete(_directory);
    }
}

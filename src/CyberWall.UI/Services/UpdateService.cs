using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberWall.Common.I18n;

namespace CyberWall.UI.Services;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version? LatestVersion,
    string LatestVersionLabel,
    string ReleaseUrl,
    string? DownloadUrl,
    string? AssetName,
    string? AssetSha256,
    DateTimeOffset? PublishedAt,
    bool IsUpdateAvailable,
    string? CheckErrorKey,
    string? ReleaseNotes = null)
{
    public bool IsCheckError => CheckErrorKey is not null;
    public string StatusMessage => CheckErrorKey is { } key ? Strings.T(key) : "";
    public string? PackageError =>
        !UpdateService.IsGitHubUrl(DownloadUrl) || !UpdateService.IsPackageName(AssetName) ? "UpdateNoPackage" :
        !UpdateService.IsSha256(AssetSha256) ? "UpdateVerifyUnavailable" : null;
}

public readonly record struct UpdateDownloadProgress(long Downloaded, long? Total);

public static class UpdateService
{
    private const string RepoOwner = "CyberGems";
    private const string RepoName = "CyberWall";

    public static Version GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? new Version(1, 1, 1)
            : new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    public static string GetCurrentVersionLabel()
    {
        var v = GetCurrentVersion();
        return v.Revision > 0 ? $"v{v}" : $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    public static string GetRuntimeChannel() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        _ => "win-x64"
    };

    private static readonly HttpClient GitHubHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        GitHubHttp.DefaultRequestHeaders.UserAgent.ParseAdd("CyberWall");
        GitHubHttp.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            CyberWall.Service.Wfp.RealFirewall.EnsureSelfAllowed();
            var json = await GitHubHttp.GetStringAsync(
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest",
                cancellationToken).ConfigureAwait(false);

            return ParseRelease(json, currentVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CheckFailed("UpdateCheckTimeout");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Update check: {ex}");
            return CheckFailed("UpdateCheckConnectionError");
        }
    }

    internal static UpdateCheckResult CheckFailed(string errorKey) =>
        new(GetCurrentVersion(), null, GetCurrentVersionLabel(), "", null, null, null, null, false, errorKey);

    internal static UpdateCheckResult ParseRelease(string json, Version currentVersion)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
                return CheckFailed("UpdateCheckResponseError");

            var latest = new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
            var installed = new Version(currentVersion.Major, currentVersion.Minor,
                Math.Max(currentVersion.Build, 0), Math.Max(currentVersion.Revision, 0));
            var releaseUrl = root.GetProperty("html_url").GetString() ?? "";
            DateTimeOffset? publishedAt = root.TryGetProperty("published_at", out var published) &&
                published.ValueKind == JsonValueKind.String && published.TryGetDateTimeOffset(out var date) ? date : null;
            var notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString() : null;

            string? downloadUrl = null;
            string? assetName = null;
            string? sha256 = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                int bestPriority = int.MaxValue;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (!IsPackageName(name)) continue;
                    var candidate = asset.GetProperty("browser_download_url").GetString();
                    if (!IsGitHubUrl(candidate)) continue;
                    int priority = PackagePriority(name!);
                    if (priority >= bestPriority) continue;

                    bestPriority = priority;
                    downloadUrl = candidate;
                    assetName = name;
                    sha256 = ReadAssetDigest(asset) ?? ExtractHashFromBody(notes, name!);
                }
            }

            return new UpdateCheckResult(installed, latest, tag, releaseUrl,
                downloadUrl, assetName, sha256, publishedAt, latest > installed, null, notes);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Debug.WriteLine($"Unexpected release data: {ex}");
            return CheckFailed("UpdateCheckResponseError");
        }
    }

    private static int PackagePriority(string name)
    {
        var channel = GetRuntimeChannel();
        bool matchesChannel = name.Contains(channel, StringComparison.OrdinalIgnoreCase);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return matchesChannel ? 2 : int.MaxValue;
        if (matchesChannel) return 0;
        return name.Contains("win-", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 1;
    }

    internal static bool IsGitHubUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort &&
        uri.AbsolutePath.StartsWith($"/{RepoOwner}/{RepoName}/releases/", StringComparison.OrdinalIgnoreCase);

    internal static bool IsPackageName(string? name) =>
        !string.IsNullOrEmpty(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        (name.StartsWith("CyberWall-Setup-", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
         name.StartsWith("CyberWall", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    internal static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Reads the checksum published for a release asset. GitHub exposes it as "digest"
    /// ("sha256:&lt;hex&gt;"); older payloads used a plain "sha256" field.
    /// </summary>
    private static string? ReadAssetDigest(JsonElement asset)
    {
        if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String)
        {
            var value = digest.GetString()?.Trim();
            if (value?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
                value = value["sha256:".Length..].Trim();
            if (IsSha256(value)) return value;
        }
        if (asset.TryGetProperty("sha256", out var legacy) && legacy.ValueKind == JsonValueKind.String)
        {
            var value = legacy.GetString()?.Trim();
            if (IsSha256(value)) return value;
        }
        return null;
    }

    private static string? ExtractHashFromBody(string? body, string assetName)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = Regex.Match(body,
            $@"{Regex.Escape(assetName)}[^\r\n]*?\b([0-9a-f]{{64}})\b", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        // The suite release template publishes separate installer and portable hashes.
        var label = assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? @"(?:\*\*Installer\*\*|Setup Installer)"
            : @"\*\*Portable\*\*";
        match = Regex.Match(body, $@"^{label}[^\r\n]*?\b([0-9a-f]{{64}})\b",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static async Task<bool> VerifyDownloadAsync(string path, string? expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (!IsSha256(expectedSha256)) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(Convert.ToHexString(hash), expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    public static Task DownloadVerifiedAsync(string downloadUrl, string destinationPath, string? expectedSha256,
        IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken = default)
    {
        if (!IsGitHubUrl(downloadUrl))
            throw new InvalidOperationException(Strings.T("UpdateNoPackage"));

        return DownloadAssetAsync(GitHubHttp, new Uri(downloadUrl), destinationPath, expectedSha256, progress,
            cancellationToken);
    }

    internal static async Task DownloadAssetAsync(HttpClient http, Uri url, string destinationPath,
        string? expectedSha256, IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken = default)
    {
        if (!IsSha256(expectedSha256))
            throw new InvalidOperationException(Strings.T("UpdateVerifyUnavailable"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        var stagingPath = $"{destinationPath}.{Guid.NewGuid():N}.download";
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            long downloaded = 0;
            progress.Report(new(0, total));
            var timer = Stopwatch.StartNew();
            await using (var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var file = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, useAsync: true))
            {
                var buffer = new byte[64 * 1024];
                int count;
                while ((count = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    downloaded += count;
                    if (timer.ElapsedMilliseconds < 120) continue;
                    progress.Report(new(downloaded, total));
                    timer.Restart();
                }
            }

            if (total is { } size && size != downloaded)
                throw new IOException("The update download ended before the expected size.");
            progress.Report(new(downloaded, total));
            if (!await VerifyDownloadAsync(stagingPath, expectedSha256, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException(Strings.T("UpdateVerifyFailed"));

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, destinationPath, overwrite: true);
        }
        finally
        {
            try { File.Delete(stagingPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Cannot remove the partial update: {ex}");
            }
        }
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath) || !Path.GetExtension(installerPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update installer is missing or invalid.");

        using var installer = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("The update installer did not start.");

        System.Windows.Application.Current.Shutdown();
    }
}

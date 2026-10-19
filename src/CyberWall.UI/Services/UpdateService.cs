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
    string StatusMessage);

public static class UpdateService
{
    private const string RepoOwner = "CyberGems";
    private const string RepoName = "CyberWall";

    public static UpdateCheckResult? LastCheckResult { get; private set; }
    public static event Action<UpdateCheckResult>? UpdateAvailabilityChanged;

    private static UpdateCheckResult RecordResult(UpdateCheckResult res)
    {
        LastCheckResult = res;
        try { UpdateAvailabilityChanged?.Invoke(res); } catch { }
        return res;
    }

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

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        var currentLabel = GetCurrentVersionLabel();

        try
        {
            CyberWall.Service.Wfp.RealFirewall.EnsureSelfAllowed();
            var json = await GitHubHttp.GetStringAsync(
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest",
                cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = root.GetProperty("html_url").GetString() ?? "";
            DateTimeOffset? publishedAt = root.TryGetProperty("published_at", out var pub) ? pub.GetDateTimeOffset() : null;

            var latestVersionStr = tagName.TrimStart('v');
            if (!Version.TryParse(latestVersionStr, out var latestVersion))
                latestVersion = null;

            string? downloadUrl = null;
            string? assetName = null;
            string? assetSha256 = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                // Search for the setup installer first
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.StartsWith("CyberWall-Setup-", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = name;
                        assetSha256 = ReadAssetDigest(asset);
                        break;
                    }
                }

                // Fallback to architecture-specific channel (e.g. portable or channel zip)
                if (downloadUrl is null)
                {
                    var channel = GetRuntimeChannel();
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.Contains(channel, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            assetName = name;
                            assetSha256 = ReadAssetDigest(asset);
                            break;
                        }
                    }
                }
            }

            // The release body also carries the checksum published by CI, used when the
            // API does not expose the asset digest.
            if (string.IsNullOrEmpty(assetSha256) && root.TryGetProperty("body", out var body))
            {
                assetSha256 = ExtractHashFromBody(body.GetString() ?? "", assetName);
            }

            if (downloadUrl is null && root.TryGetProperty("zipball_url", out var zip))
                downloadUrl = zip.GetString();

            var isAvailable = latestVersion is not null && latestVersion > currentVersion;
            var status = isAvailable
                ? Strings.T("UpdateAvailable", tagName)
                : Strings.T("UpToDate", currentLabel);

            return RecordResult(new UpdateCheckResult(
                currentVersion,
                latestVersion,
                tagName,
                releaseUrl,
                downloadUrl,
                assetName,
                assetSha256,
                publishedAt,
                isAvailable,
                status));
        }
        catch (HttpRequestException)
        {
            return RecordResult(new UpdateCheckResult(
                currentVersion, null, currentLabel, string.Empty, null, null, null, null, false,
                Strings.Current == Lang.Es ? "No se pudo comprobar actualizaciones. Comprueba tu conexión a internet." : "Could not check for updates. Check your internet connection."));
        }
        catch (TaskCanceledException)
        {
            return RecordResult(new UpdateCheckResult(
                currentVersion, null, currentLabel, string.Empty, null, null, null, null, false,
                Strings.Current == Lang.Es ? "La comprobación de actualizaciones expiró." : "Update check timed out. Try again later."));
        }
        catch (JsonException)
        {
            return RecordResult(new UpdateCheckResult(
                currentVersion, null, currentLabel, string.Empty, null, null, null, null, false,
                Strings.Current == Lang.Es ? "Respuesta inesperada del servidor de actualizaciones." : "Unexpected response from update server."));
        }
    }

    /// <summary>
    /// Reads the checksum published for a release asset. GitHub exposes it as "digest"
    /// ("sha256:&lt;hex&gt;"); older payloads used a plain "sha256" field.
    /// </summary>
    private static string? ReadAssetDigest(JsonElement asset)
    {
        if (asset.TryGetProperty("digest", out var digest))
        {
            var value = digest.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var separator = value.IndexOf(':');
                return separator >= 0 ? value[(separator + 1)..].Trim() : value.Trim();
            }
        }
        if (asset.TryGetProperty("sha256", out var legacy) && legacy.ValueKind == JsonValueKind.String)
        {
            var value = legacy.GetString();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    /// <summary>Extracts the checksum published in the release notes for the chosen asset.</summary>
    private static string? ExtractHashFromBody(string body, string? assetName)
    {
        if (string.IsNullOrEmpty(body)) return null;
        var isInstaller = assetName != null && assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase);
        var anchor = isInstaller ? "Setup Installer" : null;
        var pattern = anchor != null
            ? $@"{Regex.Escape(anchor)}[^\r\n]*?([0-9a-fA-F]{{64}})"
            : @"([0-9a-fA-F]{64})";
        var match = Regex.Match(body, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    /// <summary>
    /// Computes the SHA256 of a downloaded file and compares it with the checksum
    /// published for the release. The installer is never launched when it does not match.
    /// </summary>
    public static async Task<bool> VerifyDownloadAsync(string path, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    public static async Task DownloadUpdateAsync(string downloadUrl, string destinationPath, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var response = await GitHubHttp.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                var percentage = (double)totalRead / totalBytes * 100.0;
                progress.Report(percentage);
            }
        }
    }

    /// <summary>
    /// Downloads a release asset and verifies its checksum before it is used.
    /// The file is discarded when the checksum does not match or cannot be verified.
    /// </summary>
    public static async Task DownloadVerifiedAsync(
        string downloadUrl,
        string destinationPath,
        string? expectedSha256,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        await DownloadUpdateAsync(downloadUrl, destinationPath, progress, cancellationToken);

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            TryDelete(destinationPath);
            throw new InvalidOperationException(Strings.T("UpdateVerifyUnavailable"));
        }

        if (!await VerifyDownloadAsync(destinationPath, expectedSha256))
        {
            TryDelete(destinationPath);
            throw new InvalidOperationException(Strings.T("UpdateVerifyFailed"));
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        };
        Process.Start(psi);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }
}

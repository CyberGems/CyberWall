using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace CyberWall.Common;

public sealed record AppIdentityInfo(
    string FileName,
    string ProductName,
    string? Publisher,
    bool IsSigned,
    bool IsMicrosoft,
    bool IsSystemPath);

public static class AppIdentity
{
    private static readonly ConcurrentDictionary<string, AppIdentityInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static AppIdentityInfo Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new AppIdentityInfo("unknown", "unknown", null, false, false, false);

        try
        {
            return Cache.GetOrAdd(path, ResolveCore);
        }
        catch
        {
            var fn = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fn)) fn = path;
            return new AppIdentityInfo(fn, fn, null, false, false, false);
        }
    }

    private static AppIdentityInfo ResolveCore(string path)
    {
        try
        {
            return ResolveCoreUnsafe(path);
        }
        catch
        {
            var fn = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fn)) fn = path;
            return new AppIdentityInfo(fn, fn, null, false, false, false);
        }
    }

    private static AppIdentityInfo ResolveCoreUnsafe(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName)) fileName = path;

        string? description = null;
        string? product = null;
        if (File.Exists(path))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                description = Clean(info.FileDescription);
                product = Clean(info.ProductName);
            }
            catch { }
        }

        string? publisher = null;
        bool signed = false;
        if (File.Exists(path))
        {
            try
            {
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                publisher = Clean(cert.GetNameInfo(X509NameType.SimpleName, false));
                signed = !string.IsNullOrEmpty(publisher);
            }
            catch { }
        }

        var systemPath = IsWindowsSystemPath(path);
        var microsoft = systemPath
            || ContainsMicrosoft(publisher)
            || ContainsMicrosoft(product);

        var hero = PickHero(fileName, description, product);
        return new AppIdentityInfo(fileName, hero, publisher, signed, microsoft, systemPath);
    }

    private static string PickHero(string fileName, string? description, string? product)
    {
        if (fileName.Equals("git-remote-https.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("git-remote-http.exe", StringComparison.OrdinalIgnoreCase))
        {
            return $"Git ({fileName})";
        }

        if (!string.IsNullOrEmpty(description) &&
            !description.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
            !description.Equals(Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        if (!string.IsNullOrEmpty(product) && !IsGenericProduct(product) &&
            !product.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
            return product;
        }

        return fileName;
    }

    private static bool IsGenericProduct(string product) =>
        product.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft Windows Operating System", StringComparison.OrdinalIgnoreCase) ||
        product.Equals("Microsoft® Windows® Operating System", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsMicrosoft(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    private static bool IsWindowsSystemPath(string path)
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows) && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return path.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Windows\SysWOW64\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\Windows\WinSxS\", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

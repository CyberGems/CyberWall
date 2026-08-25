using System.IO;

namespace CyberWall.Common;

/// <summary>
/// Resolves Microsoft Store / MSIX package identity from a filesystem path.
/// WindowsApps folders are versioned; the package family name is stable across updates.
/// </summary>
public static class PackagePath
{
    public static bool TryGetFamilyName(string? path, out string familyName)
    {
        familyName = "";
        if (!TryGetPackageFullName(path, out var fullName)) return false;

        var parts = fullName.Split('_');
        if (parts.Length < 5) return false;

        var publisherId = parts[^1];
        var name = string.Join('_', parts.Take(parts.Length - 4));
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(publisherId)) return false;

        familyName = $"{name}_{publisherId}";
        return true;
    }

    public static bool TryGetPackageDir(string? path, out string packageDir)
    {
        packageDir = "";
        if (!TryGetWindowsAppsRootAndFullName(path, out var appsRoot, out var fullName)) return false;
        packageDir = Path.Combine(appsRoot, fullName);
        return true;
    }

    private static bool TryGetPackageFullName(string? path, out string fullName)
        => TryGetWindowsAppsRootAndFullName(path, out _, out fullName);

    private static bool TryGetWindowsAppsRootAndFullName(string? path, out string appsRoot, out string fullName)
    {
        appsRoot = "";
        fullName = "";
        if (string.IsNullOrWhiteSpace(path)) return false;

        var normalized = path.Replace('/', '\\');
        const string marker = "\\windowsapps\\";
        var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        appsRoot = normalized[..(idx + marker.Length - 1)];
        var rest = normalized[(idx + marker.Length)..];
        var slash = rest.IndexOf('\\');
        fullName = slash < 0 ? rest : rest[..slash];
        if (string.IsNullOrEmpty(fullName) ||
            fullName.Equals("Deleted", StringComparison.OrdinalIgnoreCase) ||
            fullName.Equals("DeletedAllUserPackages", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

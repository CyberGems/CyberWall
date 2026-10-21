using CyberWall.Common.Geo;
using CyberWall.Service.Engine;

namespace CyberWall.UI.Services;

/// <summary>
/// Resolves the country of the connection log entries. The entries are shared by every
/// view, so the lookup happens once instead of once per view.
/// </summary>
public static class ConnectionLogGeo
{
    /// <summary>
    /// Resolves the entries that have no country yet. Pass force to look them all up
    /// again, for example when the geo database has just been downloaded.
    /// </summary>
    public static void Resolve(IReadOnlyList<ConnectionLogEntry> entries, bool force = false)
    {
        foreach (var entry in entries)
        {
            if (entry.GeoResolved && !force) continue;
            if (string.IsNullOrEmpty(entry.RemoteAddress))
            {
                entry.Geo = GeoResult.Unknown;
                entry.GeoResolved = true;
                continue;
            }
            entry.Geo = GeoCountry.Lookup(entry.RemoteAddress);
            entry.GeoResolved = true;
        }
    }
}

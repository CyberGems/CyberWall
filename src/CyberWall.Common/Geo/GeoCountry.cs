using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace CyberWall.Common.Geo;

public enum GeoKind { Unknown, Country, Local }

public readonly record struct GeoResult(GeoKind Kind, string? Iso2)
{
    public static GeoResult Unknown { get; } = new(GeoKind.Unknown, null);
    public static GeoResult Local { get; } = new(GeoKind.Local, null);
    public bool HasCountry => Kind == GeoKind.Country && Iso2 is { Length: 2 };
}

/// <summary>
/// Local IP-to-country lookup. Uses a CC0 whois/asn country database cached under
/// ProgramData (sapics/ip-location-db). No destination IPs are sent to a geo API.
/// </summary>
public static class GeoCountry
{
    private const string V4Url = "https://cdn.jsdelivr.net/npm/@ip-location-db/geo-whois-asn-country/geo-whois-asn-country-ipv4-num.csv";
    private const string V4UrlAlt = "https://unpkg.com/@ip-location-db/geo-whois-asn-country/geo-whois-asn-country-ipv4-num.csv";
    private const string V6Url = "https://cdn.jsdelivr.net/npm/@ip-location-db/geo-whois-asn-country/geo-whois-asn-country-ipv6-num.csv";
    private const string V6UrlAlt = "https://unpkg.com/@ip-location-db/geo-whois-asn-country/geo-whois-asn-country-ipv6-num.csv";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(60);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, GeoResult> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static V4Range[] _v4 = [];
    private static V6Range[] _v6 = [];
    private static Task? _loadTask;
    private static bool _ready;

    public static event Action? Updated;
    public static bool IsReady => _ready;

    public static void Warm()
    {
        _ = EnsureReadyAsync();
    }

    public static Task EnsureReadyAsync()
    {
        lock (Gate)
        {
            return _loadTask ??= Task.Run(LoadCoreAsync);
        }
    }

    public static GeoResult Lookup(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address.Trim(), out var ip))
            return GeoResult.Unknown;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IsLocal(ip))
            return GeoResult.Local;

        var key = ip.ToString();
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;
        }

        var found = ip.AddressFamily == AddressFamily.InterNetwork
            ? FindV4(ip)
            : FindV6(ip);

        lock (Gate) Cache[key] = found;
        return found;
    }

    private static bool IsLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6Multicast)
            return true;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || b[0] == 127
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 100 && b[1] is >= 64 and <= 127)
            || (b[0] >= 224 && b[0] <= 239)
            || (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255);
    }

    private static GeoResult FindV4(IPAddress ip)
    {
        var table = Volatile.Read(ref _v4);
        if (table.Length == 0) return GeoResult.Unknown;
        var value = BinaryPrimitives.ReadUInt32BigEndian(ip.GetAddressBytes());
        int lo = 0, hi = table.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            var row = table[mid];
            if (value < row.Start) hi = mid - 1;
            else if (value > row.End) lo = mid + 1;
            else return new GeoResult(GeoKind.Country, row.Iso);
        }
        return GeoResult.Unknown;
    }

    private static GeoResult FindV6(IPAddress ip)
    {
        var table = Volatile.Read(ref _v6);
        if (table.Length == 0) return GeoResult.Unknown;
        var value = ReadUInt128(ip.GetAddressBytes());
        int lo = 0, hi = table.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            var row = table[mid];
            if (value < row.Start) hi = mid - 1;
            else if (value > row.End) lo = mid + 1;
            else return new GeoResult(GeoKind.Country, row.Iso);
        }
        return GeoResult.Unknown;
    }

    private static async Task LoadCoreAsync()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "geo");
            Directory.CreateDirectory(dir);
            var v4Path = Path.Combine(dir, "v4.bin");
            var v6Path = Path.Combine(dir, "v6.bin");

            var v4Ok = TryLoadV4(v4Path, out var v4);
            var v6Ok = TryLoadV6(v6Path, out var v6);
            if (v4Ok)
            {
                Volatile.Write(ref _v4, v4);
                if (v6Ok) Volatile.Write(ref _v6, v6);
                _ready = true;
                Updated?.Invoke();
            }

            var stale = IsStale(v4Path);
            if (!v4Ok || stale)
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("CyberWall");
                var csv4 = await DownloadTextAsync(http, V4Url, V4UrlAlt).ConfigureAwait(false);
                v4 = ParseV4(csv4);
                WriteV4(v4Path, v4);
                Volatile.Write(ref _v4, v4);
                _ready = true;
                lock (Gate) Cache.Clear();
                Updated?.Invoke();

                try
                {
                    var csv6 = await DownloadTextAsync(http, V6Url, V6UrlAlt).ConfigureAwait(false);
                    v6 = ParseV6(csv6);
                    WriteV6(v6Path, v6);
                    Volatile.Write(ref _v6, v6);
                    lock (Gate) Cache.Clear();
                    Updated?.Invoke();
                }
                catch { }
            }
        }
        catch
        {
            // Keep whatever is already in memory.
        }
    }

    private static async Task<string> DownloadTextAsync(HttpClient http, string primary, string fallback)
    {
        try
        {
            return await http.GetStringAsync(primary).ConfigureAwait(false);
        }
        catch
        {
            return await http.GetStringAsync(fallback).ConfigureAwait(false);
        }
    }

    private static bool IsStale(string path)
    {
        try
        {
            return !File.Exists(path) || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > MaxAge;
        }
        catch { return true; }
    }

    private static V4Range[] ParseV4(string csv)
    {
        var list = new List<V4Range>(200_000);
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)) continue;
            if (!uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end)) continue;
            var iso = NormalizeIso(parts[2]);
            if (iso == null) continue;
            list.Add(new V4Range(start, end, iso));
        }
        list.Sort((a, b) => a.Start.CompareTo(b.Start));
        return list.ToArray();
    }

    private static V6Range[] ParseV6(string csv)
    {
        var list = new List<V6Range>(80_000);
        using var reader = new StringReader(csv);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!TryParseUInt128(parts[0], out var start)) continue;
            if (!TryParseUInt128(parts[1], out var end)) continue;
            var iso = NormalizeIso(parts[2]);
            if (iso == null) continue;
            list.Add(new V6Range(start, end, iso));
        }
        list.Sort((a, b) => a.Start.CompareTo(b.Start));
        return list.ToArray();
    }

    private static string? NormalizeIso(string raw)
    {
        var iso = raw.Trim().Trim('"').ToUpperInvariant();
        if (iso.Length != 2) return null;
        if (iso is "ZZ" or "XX" or "A1" or "A2") return null;
        if (iso[0] is < 'A' or > 'Z' || iso[1] is < 'A' or > 'Z') return null;
        return iso;
    }

    private static bool TryLoadV4(string path, out V4Range[] table)
    {
        table = [];
        try
        {
            if (!File.Exists(path)) return false;
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (br.ReadUInt32() != 0x34475743) return false; // CWG4
            int count = br.ReadInt32();
            if (count < 0 || count > 2_000_000) return false;
            table = new V4Range[count];
            for (int i = 0; i < count; i++)
            {
                var start = br.ReadUInt32();
                var end = br.ReadUInt32();
                var iso = new string(new[] { (char)br.ReadByte(), (char)br.ReadByte() });
                table[i] = new V4Range(start, end, iso);
            }
            return table.Length > 0;
        }
        catch
        {
            table = [];
            return false;
        }
    }

    private static bool TryLoadV6(string path, out V6Range[] table)
    {
        table = [];
        try
        {
            if (!File.Exists(path)) return false;
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (br.ReadUInt32() != 0x36475743) return false; // CWG6
            int count = br.ReadInt32();
            if (count < 0 || count > 2_000_000) return false;
            table = new V6Range[count];
            for (int i = 0; i < count; i++)
            {
                var start = ReadUInt128(br.ReadBytes(16));
                var end = ReadUInt128(br.ReadBytes(16));
                var iso = new string(new[] { (char)br.ReadByte(), (char)br.ReadByte() });
                table[i] = new V6Range(start, end, iso);
            }
            return table.Length > 0;
        }
        catch
        {
            table = [];
            return false;
        }
    }

    private static void WriteV4(string path, V4Range[] table)
    {
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(0x34475743);
            bw.Write(table.Length);
            foreach (var row in table)
            {
                bw.Write(row.Start);
                bw.Write(row.End);
                bw.Write((byte)row.Iso[0]);
                bw.Write((byte)row.Iso[1]);
            }
        }
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }

    private static void WriteV6(string path, V6Range[] table)
    {
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(0x36475743);
            bw.Write(table.Length);
            var buf = new byte[16];
            foreach (var row in table)
            {
                WriteUInt128(buf, row.Start);
                bw.Write(buf);
                WriteUInt128(buf, row.End);
                bw.Write(buf);
                bw.Write((byte)row.Iso[0]);
                bw.Write((byte)row.Iso[1]);
            }
        }
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }

    private static UInt128 ReadUInt128(ReadOnlySpan<byte> bytes) =>
        new(BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]), BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));

    private static void WriteUInt128(Span<byte> dest, UInt128 value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(dest[..8], value.GetUpper());
        BinaryPrimitives.WriteUInt64BigEndian(dest[8..], value.GetLower());
    }

    private static bool TryParseUInt128(string text, out UInt128 value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return UInt128.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private readonly record struct V4Range(uint Start, uint End, string Iso);
    private readonly record struct V6Range(UInt128 Start, UInt128 End, string Iso);
}

file static class UInt128Ext
{
    public static ulong GetUpper(this UInt128 value) => (ulong)(value >> 64);
    public static ulong GetLower(this UInt128 value) => (ulong)value;
}

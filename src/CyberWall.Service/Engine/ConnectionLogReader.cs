using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.Models;

namespace CyberWall.Service.Engine;

/// <summary>One parsed entry of the connection log.</summary>
public sealed class ConnectionLogEntry
{
    public DateTime Timestamp { get; set; }
    public string TimestampText { get; set; } = "";
    public Verdict Verdict { get; set; }
    public Direction Direction { get; set; }
    public string AppPath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RemoteEndpoint { get; set; } = "";
    public string RemoteAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string ProcessId { get; set; } = "";
    public string RawLine { get; set; } = "";

    /// <summary>Country of the destination, resolved by the user interface layer.</summary>
    public GeoResult Geo { get; set; } = GeoResult.Unknown;

    /// <summary>Whether the country was already looked up for this entry.</summary>
    public bool GeoResolved { get; set; }
}

/// <summary>
/// Reads and parses the connection log once for the whole application.
///
/// The log is append only between trims, so every refresh only parses the bytes that
/// were added since the previous one. All the views share the result, which removes
/// the repeated full reads and re-parses of a file that can be a couple of megabytes.
/// </summary>
public static class ConnectionLogReader
{
    private static readonly object Lock = new();
    private static readonly List<ConnectionLogEntry> Entries = new();

    private static ConnectionLogEntry[] _snapshot = Array.Empty<ConnectionLogEntry>();
    private static long _offset;
    private static string _firstLine = "";
    private static bool _loaded;

    /// <summary>Upper bound for the cache, so a log that never gets trimmed cannot grow without limit.</summary>
    private const int MaxCachedEntries = 50000;

    /// <summary>Overrides the log location. Only used by the tests.</summary>
    internal static string? PathOverride { get; set; }

    private static string LogPath => PathOverride ?? BlockedLog.LogPath;

    public static int Count
    {
        get { lock (Lock) return Entries.Count; }
    }

    /// <summary>
    /// Returns the parsed entries. Only the lines appended since the last call are
    /// read and parsed. The result is a stable snapshot, safe to iterate.
    /// </summary>
    public static IReadOnlyList<ConnectionLogEntry> Refresh()
    {
        lock (Lock)
        {
            var path = LogPath;
            if (!File.Exists(path))
            {
                ClearCache();
                return _snapshot;
            }

            try
            {
                var length = new FileInfo(path).Length;
                if (!_loaded || length < _offset)
                {
                    // The file was cleared or trimmed: it has to be read again.
                    Reload(path);
                }
                else if (length > _offset)
                {
                    Append(path, length);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectionLogReader: {ex.Message}");
            }

            return _snapshot;
        }
    }

    /// <summary>Drops the cached entries, for example after the log has been cleared.</summary>
    public static void Reset()
    {
        lock (Lock) ClearCache();
    }

    private static void ClearCache()
    {
        Entries.Clear();
        _snapshot = Array.Empty<ConnectionLogEntry>();
        _offset = 0;
        _firstLine = "";
        _loaded = false;
    }

    private static void Reload(string path)
    {
        Entries.Clear();
        _offset = 0;
        _firstLine = "";

        var parsed = new List<ConnectionLogEntry>();
        LoadRange(path, 0, parsed, out var newOffset, out var firstLine);

        Entries.AddRange(parsed);
        _offset = newOffset;
        _firstLine = firstLine;
        _loaded = true;
        TrimCache();
        RebuildSnapshot();
    }

    private static void Append(string path, long length)
    {
        var parsed = new List<ConnectionLogEntry>();
        LoadRange(path, _offset, parsed, out var newOffset, out _);

        if (parsed.Count > 0 && !LooksLikeLogLine(parsed[0]))
        {
            // The offset did not land on a line boundary, so the whole file is read
            // again instead of returning corrupted entries.
            Reload(path);
            return;
        }

        Entries.AddRange(parsed);
        _offset = newOffset;
        TrimCache();
        RebuildSnapshot();
    }

    /// <summary>
    /// Reads the lines from a byte offset and parses them. Only complete lines are
    /// consumed, so a partially written line is picked up on the next refresh.
    /// </summary>
    private static void LoadRange(string path, long startOffset, List<ConnectionLogEntry> target, out long newOffset, out string firstLine)
    {
        newOffset = startOffset;
        firstLine = "";

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (startOffset > 0) fs.Seek(startOffset, SeekOrigin.Begin);

        var newlineBytes = DetectNewlineBytes(fs);
        var endsWithNewline = EndsWithNewline(fs);
        using var sr = new StreamReader(fs, Encoding.UTF8);

        long consumed = startOffset;

        // The last line is held back, because it is only complete when the file ends
        // with a line break.
        string? pending = null;
        while (sr.ReadLine() is { } line)
        {
            if (pending != null)
            {
                consumed += Consume(pending, target, ref firstLine, newlineBytes);
            }
            pending = line;
        }

        if (pending != null && endsWithNewline)
        {
            consumed += Consume(pending, target, ref firstLine, newlineBytes);
        }

        newOffset = consumed;
    }

    /// <summary>Adds a parsed line to the target and returns the bytes it used.</summary>
    private static int Consume(string line, List<ConnectionLogEntry> target, ref string firstLine, int newlineBytes)
    {
        if (target.Count == 0 && firstLine.Length == 0) firstLine = line;
        if (TryParse(line, out var entry) && entry != null) target.Add(entry);
        return Encoding.UTF8.GetByteCount(line) + newlineBytes;
    }

    /// <summary>Checks whether the file ends with a line break.</summary>
    private static bool EndsWithNewline(FileStream fs)
    {
        try
        {
            if (fs.Length == 0) return true;
            var position = fs.Position;
            fs.Seek(-1, SeekOrigin.End);
            var last = fs.ReadByte();
            fs.Position = position;
            return last == '\n';
        }
        catch { return true; }
    }

    /// <summary>Detects whether the file uses CRLF or LF line endings.</summary>
    private static int DetectNewlineBytes(FileStream fs)
    {
        try
        {
            var position = fs.Position;
            var buffer = new byte[512];
            var read = fs.Read(buffer, 0, buffer.Length);
            fs.Position = position;

            for (int i = 1; i < read; i++)
            {
                if (buffer[i] != (byte)'\n') continue;
                return buffer[i - 1] == (byte)'\r' ? 2 : 1;
            }
        }
        catch { }
        return 2;
    }

    /// <summary>Checks that a line starts with the timestamp, which proves the offset was right.</summary>
    private static bool LooksLikeLogLine(ConnectionLogEntry entry)
    {
        if (entry.TimestampText.Length < 19) return false;
        return DateTime.TryParseExact(entry.TimestampText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static void TrimCache()
    {
        if (Entries.Count <= MaxCachedEntries) return;
        var remove = Entries.Count - MaxCachedEntries;
        Entries.RemoveRange(0, remove);
    }

    private static void RebuildSnapshot()
    {
        _snapshot = Entries.ToArray();
    }

    private static bool TryParse(string? line, out ConnectionLogEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 5) return false;

        var timestampText = parts[0];
        var endpoint = parts[4];

        var address = NetworkEndpoint.ExtractAddress(endpoint) ?? "";
        var port = 0;
        var colon = endpoint.LastIndexOf(':');
        if (colon >= 0 && int.TryParse(endpoint[(colon + 1)..], out var parsedPort))
            port = parsedPort;

        entry = new ConnectionLogEntry
        {
            Timestamp = ParseTimestamp(timestampText),
            TimestampText = timestampText,
            Verdict = parts[1].Equals("Block", StringComparison.OrdinalIgnoreCase) ? Verdict.Block : Verdict.Allow,
            Direction = parts[2].Equals("Inbound", StringComparison.OrdinalIgnoreCase) ? Direction.Inbound : Direction.Outbound,
            AppPath = parts[3],
            DisplayName = SafeFileName(parts[3]),
            RemoteEndpoint = endpoint,
            RemoteAddress = address,
            RemotePort = port,
            ProcessId = parts.Length > 5 ? parts[5] : "",
            RawLine = line,
        };
        return true;
    }

    private static DateTime ParseTimestamp(string text)
    {
        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
            return invariant;
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var current))
            return current;
        return DateTime.Now;
    }

    private static string SafeFileName(string path)
    {
        try { return Path.GetFileName(path); }
        catch { return path; }
    }
}

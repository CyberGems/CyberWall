using System.Diagnostics;
using System.IO;
using System.Text;
using CyberWall.Common.IO;
using CyberWall.Common.Models;

namespace CyberWall.Service.Engine;

public static class BlockedLog
{
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "blocked.log");
    private static readonly object Lock = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private const long MaxSizeBytes = 2 * 1024 * 1024; // 2 MB cap
    private const int TargetLinesAfterTrim = 4000;
    private const int TrimEveryAppends = 64;
    private static int _appendCounter = 0;

    public static void Append(ConnectionEvent ev, Verdict verdict)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ev.AppPath)) return;
            if (string.IsNullOrWhiteSpace(ev.RemoteAddress) || ev.RemoteAddress.StartsWith("0.0.0.0") || ev.RemoteAddress.StartsWith("::")) return;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {verdict} | {ev.Direction} | {ev.AppPath} | {ev.RemoteAddress}:{ev.RemotePort} | PID {ev.ProcessId}{Environment.NewLine}";
            lock (Lock)
            {
                // The handle is closed before the trim, so the file can be replaced.
                using (var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, Utf8NoBom))
                {
                    sw.Write(line);
                }

                if (++_appendCounter % TrimEveryAppends == 0)
                {
                    EnsureCapacityLocked();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BlockedLog: could not append to the log: {ex.Message}");
        }
    }

    private static void EnsureCapacityLocked()
    {
        try
        {
            var fi = new FileInfo(Path);
            if (!fi.Exists || fi.Length <= MaxSizeBytes) return;

            var lines = new List<string>(TargetLinesAfterTrim * 2);
            using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                while (sr.ReadLine() is { } l)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    // A single oversized line is the sign of a corrupted file: it is
                    // dropped instead of being kept and parsed as a valid record.
                    if (l.Length > 4096) continue;
                    lines.Add(l);
                }
            }

            if (lines.Count > TargetLinesAfterTrim)
            {
                lines = lines.Skip(lines.Count - TargetLinesAfterTrim).ToList();
            }

            var content = lines.Count > 0
                ? string.Join(Environment.NewLine, lines) + Environment.NewLine
                : string.Empty;

            // The atomic replace is preferred, but the file may be open in the user
            // interface. The in place rewrite is the fallback so the log is always
            // brought back to its size limit.
            try
            {
                AtomicFile.WriteAllText(Path, content, Utf8NoBom);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BlockedLog: atomic trim failed: {ex.Message}");
            }

            if (new FileInfo(Path).Length > MaxSizeBytes)
            {
                using var fs = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, Utf8NoBom);
                sw.Write(content);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BlockedLog: could not trim the log: {ex.Message}");
        }
    }

    public static void Clear()
    {
        try
        {
            lock (Lock)
            {
                // Opened with FileShare.Read so it cannot race with a concurrent append.
                File.WriteAllText(Path, string.Empty, Utf8NoBom);
            }
            TryDeleteTempFile();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BlockedLog: could not clear the log: {ex.Message}");
        }
    }

    /// <summary>Removes the temporary file left behind by an interrupted trim.</summary>
    public static void TryDeleteTempFile()
    {
        try
        {
            var temp = Path + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
        }
        catch { }
    }

    public static string LogPath => Path;
}

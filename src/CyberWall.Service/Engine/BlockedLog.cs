using System.IO;
using System.Text;
using CyberWall.Common.Models;

namespace CyberWall.Service.Engine;

public static class BlockedLog
{
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "blocked.log");
    private static readonly object Lock = new();
    private const long MaxSizeBytes = 4 * 1024 * 1024; // 4 MB cap
    private const int TargetLinesAfterTrim = 8000;
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
                using var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, Encoding.UTF8);
                sw.Write(line);

                if (++_appendCounter % 128 == 0)
                {
                    EnsureCapacityLocked();
                }
            }
        }
        catch { }
    }

    private static void EnsureCapacityLocked()
    {
        try
        {
            var fi = new FileInfo(Path);
            if (!fi.Exists || fi.Length <= MaxSizeBytes) return;

            var lines = new List<string>(12000);
            using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                while (sr.ReadLine() is { } l)
                {
                    if (!string.IsNullOrWhiteSpace(l)) lines.Add(l);
                }
            }

            if (lines.Count > TargetLinesAfterTrim)
            {
                var keep = lines.Skip(lines.Count - TargetLinesAfterTrim);
                using var fs = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, Encoding.UTF8);
                foreach (var line in keep)
                {
                    sw.WriteLine(line);
                }
            }
        }
        catch { }
    }

    public static void Clear()
    {
        try
        {
            lock (Lock)
            {
                if (File.Exists(Path))
                {
                    using var fs = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                }
            }
        }
        catch { }
    }

    public static string LogPath => Path;
}

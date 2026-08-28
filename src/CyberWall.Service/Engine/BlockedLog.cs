using System.IO;
using CyberWall.Common.Models;

namespace CyberWall.Service.Engine;

public static class BlockedLog
{
    private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "blocked.log");
    private static readonly object Lock = new();

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
                using var sw = new StreamWriter(fs);
                sw.Write(line);
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

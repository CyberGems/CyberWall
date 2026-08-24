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
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {verdict} | {ev.Direction} | {ev.AppPath} | {ev.RemoteAddress}:{ev.RemotePort} | PID {ev.ProcessId}{Environment.NewLine}";
            lock (Lock) File.AppendAllText(Path, line);
        }
        catch { }
    }

    public static string LogPath => Path;
}

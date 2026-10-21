using System.Text;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;

namespace CyberWall.Tests;

/// <summary>
/// The connection log is read once for the whole application and only the lines that
/// were appended since the last read are parsed. These tests cover the incremental
/// behaviour and the recovery when the log is trimmed or cleared.
/// </summary>
[Collection("ConnectionLog")]
public class ConnectionLogReaderTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConnectionLogReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "CyberWallTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "blocked.log");

        ConnectionLogReader.PathOverride = _path;
        ConnectionLogReader.Reset();
    }

    public void Dispose()
    {
        ConnectionLogReader.PathOverride = null;
        ConnectionLogReader.Reset();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static string Line(string timestamp, string verdict, string direction, string app, string endpoint, int pid)
        => $"{timestamp} | {verdict} | {direction} | {app} | {endpoint} | PID {pid}";

    private void Write(params string[] lines)
    {
        File.WriteAllText(_path, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
    }

    private void Append(string line)
    {
        File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
    }

    [Fact]
    public void TheEntriesAreParsed()
    {
        Write(Line("2026-10-20 10:00:00", "Block", "Outbound", @"C:\Apps\Demo.exe", "93.184.216.34:443", 1234));

        var entries = ConnectionLogReader.Refresh();

        Assert.Single(entries);
        var entry = entries[0];
        Assert.Equal(Verdict.Block, entry.Verdict);
        Assert.Equal(Direction.Outbound, entry.Direction);
        Assert.Equal(@"C:\Apps\Demo.exe", entry.AppPath);
        Assert.Equal("Demo.exe", entry.DisplayName);
        Assert.Equal("93.184.216.34:443", entry.RemoteEndpoint);
        Assert.Equal("93.184.216.34", entry.RemoteAddress);
        Assert.Equal(443, entry.RemotePort);
        Assert.Equal(new DateTime(2026, 10, 20, 10, 0, 0), entry.Timestamp);
    }

    [Fact]
    public void TheInboundDirectionIsRecognised()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Inbound", @"C:\Apps\Demo.exe", "10.0.0.5:8080", 7));

        var entry = ConnectionLogReader.Refresh()[0];

        Assert.Equal(Direction.Inbound, entry.Direction);
        Assert.Equal(Verdict.Allow, entry.Verdict);
    }

    [Fact]
    public void MalformedLinesAreIgnored()
    {
        Write(
            "not a valid line",
            "",
            Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\Demo.exe", "1.2.3.4:80", 9));

        var entries = ConnectionLogReader.Refresh();

        Assert.Single(entries);
    }

    [Fact]
    public void OnlyTheAppendedLinesAreParsedOnARefresh()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1));
        var first = ConnectionLogReader.Refresh();
        Assert.Single(first);

        Append(Line("2026-10-20 10:01:00", "Block", "Outbound", @"C:\Apps\Two.exe", "2.2.2.2:80", 2));

        var second = ConnectionLogReader.Refresh();
        Assert.Equal(2, second.Count);
        Assert.Equal("Two.exe", second[1].DisplayName);

        // The first entry is the same object, it was not parsed again.
        Assert.Same(first[0], second[0]);
    }

    [Fact]
    public void TheSnapshotIsReusedWhenNothingWasAppended()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1));

        var first = ConnectionLogReader.Refresh();
        var second = ConnectionLogReader.Refresh();

        Assert.Same(first, second);
    }

    [Fact]
    public void ATrimmedLogIsReadAgain()
    {
        for (var i = 0; i < 50; i++)
            Append(Line("2026-10-20 10:00:00", "Allow", "Outbound", $@"C:\Apps\App{i}.exe", "1.1.1.1:80", i));

        var before = ConnectionLogReader.Refresh();
        Assert.Equal(50, before.Count);

        // The log is trimmed: it keeps the last lines and starts over.
        Write(Line("2026-10-20 11:00:00", "Block", "Outbound", @"C:\Apps\New.exe", "3.3.3.3:80", 99));

        var after = ConnectionLogReader.Refresh();
        Assert.Single(after);
        Assert.Equal("New.exe", after[0].DisplayName);
    }

    [Fact]
    public void AclearedLogEmptiesTheEntries()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1));
        Assert.Single(ConnectionLogReader.Refresh());

        File.WriteAllText(_path, string.Empty);

        Assert.Empty(ConnectionLogReader.Refresh());
    }

    [Fact]
    public void AMissingLogEmptiesTheEntries()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1));
        Assert.Single(ConnectionLogReader.Refresh());

        File.Delete(_path);

        Assert.Empty(ConnectionLogReader.Refresh());
    }

    [Fact]
    public void AnIncompleteLastLineIsReadOnTheNextRefresh()
    {
        Write(Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1));

        // A line that is still being written.
        File.AppendAllText(_path, "2026-10-20 10:01:00 | Block | Outbo", new UTF8Encoding(false));

        var partial = ConnectionLogReader.Refresh();
        Assert.Single(partial);

        // The rest of the line arrives.
        File.AppendAllText(_path, "und | C:\\Apps\\Two.exe | 2.2.2.2:80 | PID 2" + Environment.NewLine, new UTF8Encoding(false));

        var complete = ConnectionLogReader.Refresh();
        Assert.Equal(2, complete.Count);
        Assert.Equal("Two.exe", complete[1].DisplayName);
        Assert.Equal(Verdict.Block, complete[1].Verdict);
    }

    [Fact]
    public void TheEntriesKeepTheirOrder()
    {
        Write(
            Line("2026-10-20 10:00:00", "Allow", "Outbound", @"C:\Apps\One.exe", "1.1.1.1:80", 1),
            Line("2026-10-20 10:01:00", "Allow", "Outbound", @"C:\Apps\Two.exe", "2.2.2.2:80", 2),
            Line("2026-10-20 10:02:00", "Allow", "Outbound", @"C:\Apps\Three.exe", "3.3.3.3:80", 3));

        var entries = ConnectionLogReader.Refresh();

        Assert.Equal("One.exe", entries[0].DisplayName);
        Assert.Equal("Two.exe", entries[1].DisplayName);
        Assert.Equal("Three.exe", entries[2].DisplayName);
    }

    [Fact]
    public void TheTimestampIsParsedWithTheFormatTheWriterUses()
    {
        Write(Line("2026-10-20 08:07:21", "Allow", "Outbound", @"C:\Apps\Demo.exe", "1.1.1.1:80", 1));

        var entry = ConnectionLogReader.Refresh()[0];

        Assert.Equal(new DateTime(2026, 10, 20, 8, 7, 21), entry.Timestamp);
        Assert.Equal("2026-10-20 08:07:21", entry.TimestampText);
    }
}

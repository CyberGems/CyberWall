using System.IO.Pipes;
using System.Text.Json;
using CyberWall.Common.Ipc;
using CyberWall.Common.Models;

namespace CyberWall.Service.Ipc;

public sealed class PipeServer : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _loop;
    public event Func<ConnectionEvent, Task<VerdictReply>>? OnAsk;
    public Func<IReadOnlyCollection<AppRule>>? GetRules;
    public Action<AppRule>? OnUpsert;
    public Action<string>? OnRemove;

    public void Start()
    {
        _cts = new();
        _loop = Task.Run(() => Loop(_cts.Token));
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(PipeProtocol.PipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try { await server.WaitForConnectionAsync(ct); _ = Handle(server, ct); }
            catch { server.Dispose(); await Task.Delay(500, ct); }
        }
    }

    private async Task Handle(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using (pipe)
        using (var r = new StreamReader(pipe))
        using (var w = new StreamWriter(pipe) { AutoFlush = true })
        {
            while (!ct.IsCancellationRequested && pipe.IsConnected)
            {
                var line = await r.ReadLineAsync(ct);
                if (line == null) break;
                var msg = JsonSerializer.Deserialize<IpcMessage>(line);
                if (msg == null) continue;
                if (msg.Type == IpcTypes.Ping) await w.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage { Type = "pong" }));
                else if (msg.Type == IpcTypes.RulesSync && GetRules != null)
                {
                    var rules = GetRules();
                    await w.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage { Type = IpcTypes.RulesSync, PayloadJson = JsonSerializer.Serialize(rules) }));
                }
            }
        }
    }

    public async Task<VerdictReply?> AskUi(ConnectionEvent ev)
    {
        if (OnAsk == null) return null;
        return await OnAsk(ev);
    }

    public void Dispose() { _cts?.Cancel(); try { _loop?.Wait(800); } catch { } _cts?.Dispose(); }
}

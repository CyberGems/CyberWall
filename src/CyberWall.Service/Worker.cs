using Microsoft.Extensions.Hosting;
using CyberWall.Service.Engine;

namespace CyberWall.Service;

public sealed class Worker : BackgroundService
{
    private readonly FirewallService _fw = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _fw.Enable(Common.Models.FirewallMode.Ask);
        await Task.Delay(Timeout.Infinite, ct);
    }

    public override Task StopAsync(CancellationToken ct) { _fw.Disable(); return base.StopAsync(ct); }
}

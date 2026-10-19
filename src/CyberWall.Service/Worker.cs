using System.Diagnostics;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;
using Microsoft.Extensions.Hosting;

namespace CyberWall.Service;

/// <summary>
/// Background host of the firewall engine.
///
/// The engine runs in one place only. When the desktop application is open it owns
/// the engine and the service steps aside, so both never rewrite the same Windows
/// Firewall rules. When the application is closed the service takes over using the
/// mode and the state persisted in the settings file.
/// </summary>
public sealed class Worker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly FirewallService _fw = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Worker: {ex.Message}");
            }

            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Tick()
    {
        var uiRunning = EngineOwnership.IsUiRunning();

        if (uiRunning)
        {
            // The interactive session owns the engine while it is open.
            if (_fw.IsEnabled) _fw.Disable();
            return;
        }

        var settings = AppSettings.Load();

        if (!settings.FirewallEnabled)
        {
            if (_fw.IsEnabled) _fw.Disable();
            return;
        }

        if (!_fw.IsEnabled)
        {
            _fw.Enable(settings.FirewallMode);
            return;
        }

        // The desktop application may have changed the rules while it was running.
        if (_fw.Store.ReloadIfChanged())
        {
            _fw.ReapplyAllowRules();
        }
    }

    public override Task StopAsync(CancellationToken ct)
    {
        _fw.Disable();
        return base.StopAsync(ct);
    }
}

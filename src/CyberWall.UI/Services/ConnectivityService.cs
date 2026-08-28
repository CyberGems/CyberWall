using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;

namespace CyberWall.UI.Services;

public sealed class ConnectivityService
{
    private static readonly Lazy<ConnectivityService> _instance = new(() => new ConnectivityService());
    public static ConnectivityService Instance => _instance.Value;

    private readonly DispatcherTimer _timer;
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2.5)
    };

    private NotificationStore? _store;
    private int _isChecking;
    private bool _isOnline = true;
    private bool _hasCheckedAtLeastOnce;

    public bool IsOnline => _isOnline;
    public bool HasCheckedAtLeastOnce => _hasCheckedAtLeastOnce;

    public event Action<bool>? ConnectivityChanged;

    private ConnectivityService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => _ = CheckConnectivityAsync();

        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public void Start(NotificationStore store)
    {
        _store = store;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
        _ = CheckConnectivityAsync();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable)
        {
            UpdateState(false);
        }
        else
        {
            _ = CheckConnectivityAsync();
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _ = CheckConnectivityAsync();
    }

    public async Task<bool> CheckConnectivityAsync(bool force = false)
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            return _isOnline;
        }

        try
        {
            bool online = await ProbeConnectivityAsync();
            UpdateState(online);
            return online;
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    private static async Task<bool> ProbeConnectivityAsync()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            bool hasActiveNic = nics.Any(nic =>
                nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (!hasActiveNic)
                return false;

            // Probe 1: Windows NCSI endpoint
            try
            {
                using var res1 = await HttpClient.GetAsync(
                    "http://www.msftconnecttest.com/connecttest.txt",
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (res1.IsSuccessStatusCode) return true;
            }
            catch { }

            // Probe 2: Google 204 endpoint
            try
            {
                using var res2 = await HttpClient.GetAsync(
                    "http://www.google.com/generate_204",
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (res2.IsSuccessStatusCode) return true;
            }
            catch { }

            // Probe 3: Cloudflare fallback
            try
            {
                using var res3 = await HttpClient.GetAsync(
                    "http://1.1.1.1",
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (res3.IsSuccessStatusCode) return true;
            }
            catch { }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateState(bool online)
    {
        bool changed = !_hasCheckedAtLeastOnce || _isOnline != online;
        bool wasOnline = _isOnline;
        bool isFirstCheck = !_hasCheckedAtLeastOnce;

        _hasCheckedAtLeastOnce = true;
        _isOnline = online;

        if (changed)
        {
            if (_store != null)
            {
                if (!online && (wasOnline || isFirstCheck))
                {
                    _store.Add(AppNotificationKind.InternetLost);
                }
                else if (online && !wasOnline && !isFirstCheck)
                {
                    _store.Add(AppNotificationKind.InternetRestored);
                }
            }

            ConnectivityChanged?.Invoke(online);
        }
    }
}

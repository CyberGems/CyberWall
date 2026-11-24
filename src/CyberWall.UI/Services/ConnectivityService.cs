using System.Diagnostics;
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

    private static readonly object NicCacheLock = new();
    private static ulong[] _cachedActiveLuids = Array.Empty<ulong>();
    private static long _lastNicEnumTicks;
    private static volatile bool _nicsDirty = true;
    private static readonly long NicRefreshIntervalTicks = Stopwatch.Frequency * 120;

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
        _nicsDirty = true;
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
        _nicsDirty = true;
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
            if (!HasActiveNetworkInterface())
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

    internal static bool HasActiveNetworkInterface()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
            return false;

        long now = Stopwatch.GetTimestamp();
        if (!_nicsDirty)
        {
            ulong[] snapshot;
            long lastEnum;
            lock (NicCacheLock)
            {
                snapshot = _cachedActiveLuids;
                lastEnum = _lastNicEnumTicks;
            }

            if (snapshot.Length > 0 && (now - lastEnum) < NicRefreshIntervalTicks)
            {
                foreach (var luid in snapshot)
                {
                    if (NetworkSpeedService.TryReadInterfaceOctets(luid, out bool isUp, out _, out _) && isUp)
                        return true;
                }
            }
        }

        var nics = NetworkInterface.GetAllNetworkInterfaces();
        var activeLuids = new List<ulong>();
        bool hasActiveNic = false;

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            {
                hasActiveNic = true;
                if (NetworkSpeedService.TryResolveInterfaceLuid(nic.Id, out var luid))
                    activeLuids.Add(luid);
            }
        }

        lock (NicCacheLock)
        {
            _cachedActiveLuids = activeLuids.Count > 0 ? activeLuids.ToArray() : Array.Empty<ulong>();
            _lastNicEnumTicks = Stopwatch.GetTimestamp();
            _nicsDirty = false;
        }

        return hasActiveNic;
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
                    if (App.Settings.ToastInternetEventsEnabled)
                    {
                        Popup.AppInfoToast.ShowToast(
                            Common.I18n.Strings.T("NotifInternetLostTitle"),
                            Common.I18n.Strings.T("NotifInternetLostDesc"),
                            appPath: null,
                            Popup.ToastBadgeType.Danger);
                    }
                }
                else if (online && !wasOnline && !isFirstCheck)
                {
                    _store.Add(AppNotificationKind.InternetRestored);
                    if (App.Settings.ToastInternetEventsEnabled)
                    {
                        Popup.AppInfoToast.ShowToast(
                            Common.I18n.Strings.T("NotifInternetRestoredTitle"),
                            Common.I18n.Strings.T("NotifInternetRestoredDesc"),
                            appPath: null,
                            Popup.ToastBadgeType.Success);
                    }
                }
            }

            ConnectivityChanged?.Invoke(online);
        }
    }
}

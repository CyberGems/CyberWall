using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Popup;
using CyberWall.UI.Services;
using MenuItem = System.Windows.Controls.MenuItem;

namespace CyberWall.UI;

public partial class MainWindow : Window
{
    private readonly FirewallService _svc = new();
    private readonly NotificationStore _notifications = new();
    private List<AppRule> _all = new();
    private bool _loading;
    private bool _notifOpen;
    private bool _layoutRestored;
    private readonly Dictionary<string, string> _lastRemoteByApp = new(StringComparer.OrdinalIgnoreCase);
    private TrayService? _tray;

    public MainWindow()
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);
        _loading = true;
        Strings.Current = App.Settings.Language;
        ThemeManager.Apply(App.Settings.Theme);
        PromptManager.Instance.Initialize(_svc, this);
        _svc.OnAskConnection += OnAskConnection;
        _svc.OnUnknownBlocked += OnUnknownBlocked;
        _svc.OnBlockedActivity += OnBlockedActivity;
        _notifications.Changed += () => Dispatcher.BeginInvoke(UpdateNotifBadge);
        if (App.Settings.FirewallEnabled)
            _svc.Enable((FirewallMode)App.Settings.FirewallMode);
        RefreshRules();
        RefreshLanguage();
        UpdateStatus();
        var isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        if (!isAdmin) StatusText.Text += "  ⚠️ " + (Strings.Current == Lang.Es ? "Ejecuta como Admin para filtrado real" : "Run as Admin for kernel filtering");
        Closing += (_, _) => WindowLayoutPersistence.Save(this, App.Settings);
        _tray = new TrayService(this, _svc);
        GeoCountry.Updated += OnGeoUpdated;
        GeoCountry.Warm();
        ProcessTrafficTracker.Instance.ActivityUpdated += OnActivityUpdated;
        ProcessTrafficTracker.Instance.Start();
        ConnectivityService.Instance.Start(_notifications);
        AppInfoMonitorService.Instance.Start(_notifications, _svc);
        Closed += (_, _) =>
        {
            AppInfoMonitorService.Instance.Stop();
            ConnectivityService.Instance.Stop();
            ProcessTrafficTracker.Instance.ActivityUpdated -= OnActivityUpdated;
            ProcessTrafficTracker.Instance.Stop();
            GeoCountry.Updated -= OnGeoUpdated;
            App.Settings.FirewallEnabled = _svc.IsMasterOn;
            App.Settings.FirewallMode = (int)_svc.Mode;
            App.Settings.Save();
            _svc.Dispose();
        };
        StateChanged += (_, _) => Dispatcher.InvokeAsync(UpdateMaximizeButtonIcon);
        UpdateMaximizeButtonIcon();
        Loaded += (_, _) =>
        {
            if (!_layoutRestored)
            {
                WindowLayoutPersistence.Restore(this, App.Settings);
                _layoutRestored = true;
            }
            UpdateNotifBadge();
            CheckForUpdatesOnStartup();
        };
        PreviewMouseDown += (_, _) => NotifyActiveModalAttention();
        _loading = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEACTIVATE = 0x0021;
        const int WM_SETCURSOR = 0x0020;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_NCRBUTTONDOWN = 0x00A4;

        if (App.WM_SHOW_MAIN_WINDOW != 0 && msg == App.WM_SHOW_MAIN_WINDOW)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Show();
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                Focus();
            });
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_MOUSEACTIVATE)
        {
            NotifyActiveModalAttention();
        }
        else if (msg == WM_SETCURSOR)
        {
            int mouseMsg = (int)((long)lParam >> 16) & 0xFFFF;
            if (mouseMsg is WM_LBUTTONDOWN or WM_NCLBUTTONDOWN or WM_RBUTTONDOWN or WM_NCRBUTTONDOWN)
            {
                NotifyActiveModalAttention();
            }
        }

        return IntPtr.Zero;
    }

    private void NotifyActiveModalAttention()
    {
        try
        {
            foreach (Window owned in OwnedWindows)
            {
                if (owned.IsVisible)
                {
                    if (owned is IModalAttentionWindow maw)
                    {
                        maw.TriggerAttention();
                    }
                }
            }
        }
        catch { }
    }

    private bool _hasCheckedUpdates;

    internal async void CheckForUpdatesOnStartup()
    {
        _notifications.PurgeObsoleteUpdateNotifications(UpdateService.GetCurrentVersion());
        if (_svc.IsMasterOn)
            _notifications.PurgeObsoleteProtectionOffNotifications();
        if (_hasCheckedUpdates || !App.Settings.AutoCheckForUpdates) return;
        _hasCheckedUpdates = true;
        try
        {
            await Task.Delay(3000);
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                Dispatcher.Invoke(() =>
                {
                    _notifications.PurgeObsoleteUpdateNotifications(UpdateService.GetCurrentVersion());
                    _notifications.Add(AppNotificationKind.UpdateAvailable, detail: result.LatestVersionLabel);
                    var choice = ConfirmDialog.Show(
                        this,
                        Strings.T("UpdateAvailable", result.LatestVersionLabel),
                        $"{Strings.T("Current")} {UpdateService.GetCurrentVersionLabel()}\n{Strings.T("Latest")} {result.LatestVersionLabel}\n\n{Strings.T("UpdatePrompt")}",
                        Strings.T("Download"),
                        Strings.T("Later"));

                    if (choice)
                    {
                        _notifications.MarkRelatedRead(AppNotificationKind.UpdateAvailable, null);
                        var about = new AboutWindow(App.Settings) { Owner = this };
                        about.Show();
                        _ = about.StartUpdateDownloadAsync(result);
                    }
                });
            }
            else
            {
                Dispatcher.Invoke(() => _notifications.PurgeObsoleteUpdateNotifications(UpdateService.GetCurrentVersion()));
            }
        }
        catch { }
    }

    public void RefreshLanguage()
    {
        Title = Strings.T("AppTitle");
        TitleText.Text = "CyberWall";
        WfpBadgeText.Text = Strings.T("WfpEngineBadge");
        SubtitleText.Text = Strings.T("AppSubtitle");
        FooterStatusText.Text = Strings.T("StatusFooter");
        NotifBtn.ToolTip = Strings.T("Notifications");
        SettingsBtn.ToolTip = Strings.T("Settings");
        AboutBtn.ToolTip = Strings.T("About");
        ViewLogBtn.ToolTip = Strings.T("ViewLog");
        StatsBtn.ToolTip = Strings.T("StatsButton");
        ModeLbl.Text = Strings.T("Mode");
        SearchPlaceholder.Text = Strings.T("SearchPlaceholder");
        ViewLogBtnText.Text = Strings.T("ViewLog");
        StatsBtnText.Text = Strings.T("StatsButton");
        TrafficIndicator.RefreshLanguage();
        _tray?.RefreshLanguage();

        var progHdr = Strings.T("Program") + (_sortBy == "DisplayName" ? (_sortAsc ? " ▾" : " ▴") : "");
        var pathHdr = Strings.T("Path") + (_sortBy == "AppPath" ? (_sortAsc ? " ▾" : " ▴") : "");
        var actHdr = Strings.T("Action");
        var dirHdr = Strings.T("Direction") + (_sortBy == "Direction" ? (_sortAsc ? " ▾" : " ▴") : "");
        var stateHdr = Strings.T("State");
        var countryHdr = Strings.T("Country") + (_sortBy == "Country" ? (_sortAsc ? " ▾" : " ▴") : "");
        var activityHdr = Strings.T("ActivityHeader") + (_sortBy is "Activity" or "IsActiveTraffic" ? (_sortAsc ? " ▾" : " ▴") : "");

        StateHeaderText.Text = stateHdr;
        ActivityHeaderText.Text = activityHdr;
        ProgramHeaderText.Text = progHdr;
        PathHeaderText.Text = pathHdr;
        ActionHeaderText.Text = actHdr;
        CountryHeaderText.Text = countryHdr;
        DirectionHeaderText.Text = dirHdr;

        AllowColState.Header = stateHdr;
        AllowColActivity.Header = activityHdr;
        AllowColProg.Header = progHdr;
        AllowColPath.Header = pathHdr;
        AllowColAction.Header = actHdr;
        AllowColCountry.Header = countryHdr;
        AllowColDir.Header = dirHdr;

        BlockColState.Header = stateHdr;
        BlockColActivity.Header = activityHdr;
        BlockColProg.Header = progHdr;
        BlockColPath.Header = pathHdr;
        BlockColAction.Header = actHdr;
        BlockColCountry.Header = countryHdr;
        BlockColDir.Header = dirHdr;

        OnActivityUpdated();

        AllowedExpander.Header = $"{Strings.T("Allowed")} ({_all.Count(r => r.Verdict == Verdict.Allow)})";
        BlockedExpander.Header = $"{Strings.T("Blocked")} ({_all.Count(r => r.Verdict == Verdict.Block)})";
        var m = _svc.Mode switch
        {
            FirewallMode.BlockAll => 1,
            FirewallMode.Killswitch => 2,
            _ => 0
        };
        _loading = true;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeAsk") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeBlockAll") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeKillswitch") });
        ModeBox.SelectedIndex = m;
        _loading = false;
        UpdateMaximizeButtonIcon();
        UpdateCloseButtonTooltip();
    }

    public void UpdateCloseButtonTooltip()
    {
        if (CloseBtn == null) return;
        bool toTray = App.Settings.MinimizeToTrayOnClose;
        CloseBtn.ToolTip = Strings.T(toTray ? "CloseToTray" : "CloseApp");
        if (MinimizeBtn != null) MinimizeBtn.ToolTip = Strings.T("Minimize");
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (MaximizeBtn == null || MaximizeIconPath == null) return;
        if (WorkAreaMaximize.IsFilled(this) || WindowState == WindowState.Maximized)
        {
            MaximizeBtn.ToolTip = Strings.T("Restore");
            MaximizeIconPath.Data = Geometry.Parse("M 6 2 L 6 6 L 2 6 M 6 6 L 2.5 2.5 M 8 12 L 8 8 L 12 8 M 8 8 L 11.5 11.5");
        }
        else
        {
            MaximizeBtn.ToolTip = Strings.T("Maximize");
            MaximizeIconPath.Data = Geometry.Parse("M 3 7 L 3 3 L 7 3 M 3 3 L 6.5 6.5 M 11 7 L 11 11 L 7 11 M 11 11 L 7.5 7.5");
        }
    }

    public void ClearAllRulesFromSettings()
    {
        _svc.ClearAllRules();
        RefreshRules(SearchBox.Text);
    }

    public void RefreshStatusFromExternal()
    {
        UpdateStatus();
        UpdateCloseButtonTooltip();
    }

    public void ExitApplication()
    {
        _tray?.RequestExit();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WorkAreaMaximize.Toggle(this);
            UpdateMaximizeButtonIcon();
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (WorkAreaMaximize.IsFilled(this))
            {
                WorkAreaMaximize.Restore(this);
                UpdateMaximizeButtonIcon();
            }
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WorkAreaMaximize.Toggle(this);
        UpdateMaximizeButtonIcon();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnAskConnection(ConnectionEvent ev)
    {
        RememberLastRemote(ev, isBlocked: false);
        AppInfoMonitorService.Instance.CheckApp(ev.AppPath);
        PromptManager.Instance.Enqueue(ev);
    }

    private void OnUnknownBlocked(ConnectionEvent ev)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RememberLastRemote(ev, isBlocked: true);
            AppInfoMonitorService.Instance.CheckApp(ev.AppPath);
            _notifications.Add(AppNotificationKind.SilentBlock, ev.AppPath, ev.DisplayName,
                string.IsNullOrEmpty(ev.RemoteAddress) ? null : $"{ev.RemoteAddress}:{ev.RemotePort}");
        });
    }

    private void OnBlockedActivity(ConnectionEvent ev)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RememberLastRemote(ev, isBlocked: true);
            AppInfoMonitorService.Instance.CheckApp(ev.AppPath);
        });
    }

    internal void RecordAutoBlock(ConnectionEvent ev)
    {
        RememberLastRemote(ev, isBlocked: true);
        _notifications.Add(AppNotificationKind.AutoBlocked, ev.AppPath, ev.DisplayName,
            string.IsNullOrEmpty(ev.RemoteAddress) ? null : $"{ev.RemoteAddress}:{ev.RemotePort}");
        _tray?.NotifyAutoBlock(ev.DisplayName);
        if (App.Settings.ToastAutoBlockEnabled)
        {
            AutoBlockToast.ShowToast(ev, () =>
            {
                _svc.SetVerdict(ev.AppPath, Verdict.Allow, true, ev);
                _notifications.MarkRelatedRead(AppNotificationKind.AutoBlocked, ev.AppPath);
                RefreshRules(SearchBox.Text);
            });
        }
    }

    private void UpdateNotifBadge()
    {
        if (NotifBadge == null || NotifBadgeText == null) return;
        var count = _notifications.UnreadCount;
        if (count <= 0)
        {
            NotifBadge.Visibility = Visibility.Collapsed;
            return;
        }
        NotifBadgeText.Text = count > 99 ? "99+" : count.ToString();
        NotifBadge.Visibility = Visibility.Visible;
    }

    public void ShowNotifications()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (_notifOpen) return;
        _notifOpen = true;
        try
        {
            if (_svc.IsMasterOn)
                _notifications.PurgeObsoleteProtectionOffNotifications();

            var dlg = new NotificationsDialog(
                _notifications,
                async path =>
                {
                    await Task.Run(() => _svc.SetVerdict(path, Verdict.Allow, true));
                    RefreshRules(SearchBox.Text);
                },
                () =>
                {
                    if (MasterToggle.IsChecked != true)
                        MasterToggle.IsChecked = true;
                    else
                        _notifications.PurgeObsoleteProtectionOffNotifications();
                },
                OpenUpdateFromNotification,
                isProtectionOn: () => _svc.IsMasterOn,
                isOnline: () => ConnectivityService.Instance.IsOnline)
            { Owner = this };
            dlg.ShowDialog();
            if (dlg.OpenSettingsAfterClose)
                OpenSettings();
        }
        finally
        {
            _notifOpen = false;
            UpdateNotifBadge();
        }
    }

    private async void OpenUpdateFromNotification()
    {
        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            if (!result.IsUpdateAvailable) return;
            _notifications.MarkRelatedRead(AppNotificationKind.UpdateAvailable, null);
            var about = new AboutWindow(App.Settings) { Owner = this };
            about.Show();
            await about.StartUpdateDownloadAsync(result);
        }
        catch { }
    }

    private void Notifications_Click(object sender, RoutedEventArgs e) => ShowNotifications();

    private string _sortBy = "DisplayName";
    private bool _sortAsc = true;

    private void OnGeoUpdated() => Dispatcher.BeginInvoke(() => RefreshRules(SearchBox.Text));

    private DispatcherTimer? _refreshRulesDebounceTimer;
    private string? _pendingFilter;

    internal void RefreshRules(string? filter = null)
    {
        _pendingFilter = filter ?? SearchBox?.Text;
        if (_refreshRulesDebounceTimer == null)
        {
            _refreshRulesDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _refreshRulesDebounceTimer.Tick += (_, _) =>
            {
                _refreshRulesDebounceTimer.Stop();
                ExecuteRefreshRules(_pendingFilter);
            };
        }
        _refreshRulesDebounceTimer.Stop();
        _refreshRulesDebounceTimer.Start();
    }

    private void ExecuteRefreshRules(string? filter = null)
    {
        _all = _svc.Store.All.ToList();
        var last = LastRemoteByApp();
        foreach (var entry in _lastRemoteByApp)
            last[entry.Key] = entry.Value;
        var q = string.IsNullOrWhiteSpace(filter) ? _all : _all.Where(r => r.DisplayName.Contains(filter!, StringComparison.OrdinalIgnoreCase) || r.AppPath.Contains(filter!, StringComparison.OrdinalIgnoreCase)).ToList();

        IEnumerable<AppRule> SortRules(IEnumerable<AppRule> items)
        {
            if (_sortBy is "Activity" or "IsActiveTraffic" or "ActivityLevel")
            {
                return _sortAsc
                    ? items.OrderByDescending(r => (int)ProcessTrafficTracker.Instance.GetActivity(r.AppPath, r.Verdict).Level)
                           .ThenByDescending(r => ProcessTrafficTracker.Instance.GetActivity(r.AppPath, r.Verdict).ActiveSockets)
                           .ThenBy(r => r.DisplayName)
                    : items.OrderBy(r => (int)ProcessTrafficTracker.Instance.GetActivity(r.AppPath, r.Verdict).Level)
                           .ThenBy(r => r.DisplayName);
            }

            Func<AppRule, object> key = _sortBy switch
            {
                "AppPath" => r => r.AppPath,
                "Direction" => r => (int)r.EffectiveInboundVerdict * 2 + (int)r.EffectiveOutboundVerdict,
                "Country" => r =>
                {
                    string? ip = null;
                    try
                    {
                        last.TryGetValue(AppRule.Normalize(r.AppPath), out ip);
                        ip ??= last.GetValueOrDefault(r.AppPath);
                    }
                    catch
                    {
                        last.TryGetValue(r.AppPath, out ip);
                    }
                    var geo = GeoCountry.Lookup(ip);
                    if (!geo.HasCountry)
                    {
                        var activity = ProcessTrafficTracker.Instance.GetActivity(r.AppPath, r.Verdict);
                        var ep = activity.LastEndpoint ?? activity.LastBlockedEndpoint;
                        if (!string.IsNullOrEmpty(ep))
                        {
                            var liveIp = NetworkEndpoint.ExtractAddress(ep);
                            var liveGeo = GeoCountry.Lookup(liveIp);
                            if (liveGeo.HasCountry) geo = liveGeo;
                        }
                    }
                    return CountryDisplay.Label(geo);
                },
                _ => r => r.DisplayName
            };
            return _sortAsc ? items.OrderBy(key) : items.OrderByDescending(key);
        }

        var blocked = SortRules(q.Where(r => r.Verdict == Verdict.Block));
        var allowed = SortRules(q.Where(r => r.Verdict == Verdict.Allow));
        BlockedGrid.ItemsSource = blocked.Select(r => ToRow(r, last)).ToList();
        AllowedGrid.ItemsSource = allowed.Select(r => ToRow(r, last)).ToList();
        RefreshLanguage();
    }

    private void RememberLastRemote(ConnectionEvent ev, bool isBlocked = false)
    {
        if (string.IsNullOrWhiteSpace(ev.RemoteAddress) ||
            !System.Net.IPAddress.TryParse(ev.RemoteAddress, out _))
            return;

        try
        {
            var normKey = AppRule.Normalize(ev.AppPath);
            var newGeo = GeoCountry.Lookup(ev.RemoteAddress);

            // Prioritize public external country IPs: do not overwrite a known country IP with a local/private IP
            if (_lastRemoteByApp.TryGetValue(normKey, out var existingIp))
            {
                var existingGeo = GeoCountry.Lookup(existingIp);
                if (existingGeo.HasCountry && !newGeo.HasCountry)
                {
                    ProcessTrafficTracker.Instance.RecordActivity(ev.AppPath, ev.RemoteAddress, ev.RemotePort, isBlocked);
                    return;
                }
            }

            _lastRemoteByApp[normKey] = ev.RemoteAddress;
        }
        catch
        {
            _lastRemoteByApp[ev.AppPath] = ev.RemoteAddress;
        }

        ProcessTrafficTracker.Instance.RecordActivity(ev.AppPath, ev.RemoteAddress, ev.RemotePort, isBlocked);
    }

    private static AppRuleRow ToRow(AppRule rule, Dictionary<string, string> last)
    {
        string? ip = null;
        try
        {
            last.TryGetValue(AppRule.Normalize(rule.AppPath), out ip);
            ip ??= last.GetValueOrDefault(rule.AppPath);
        }
        catch
        {
            last.TryGetValue(rule.AppPath, out ip);
        }

        var geo = GeoCountry.Lookup(ip);
        if (!geo.HasCountry)
        {
            // If stored IP is local or unknown, check if live activity has a public internet destination
            var activity = ProcessTrafficTracker.Instance.GetActivity(rule.AppPath, rule.Verdict);
            var liveEp = activity.LastEndpoint ?? activity.LastBlockedEndpoint;
            if (!string.IsNullOrEmpty(liveEp))
            {
                var liveIp = NetworkEndpoint.ExtractAddress(liveEp);
                var liveGeo = GeoCountry.Lookup(liveIp);
                if (liveGeo.HasCountry)
                {
                    geo = liveGeo;
                }
            }
        }

        var row = new AppRuleRow { Rule = rule, Geo = geo };
        row.UpdateActivity(ProcessTrafficTracker.Instance);
        return row;
    }

    private void OnActivityUpdated()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnActivityUpdated);
            return;
        }

        if (AllowedGrid.ItemsSource is IEnumerable<AppRuleRow> allowedRows)
        {
            foreach (var row in allowedRows)
            {
                row.UpdateActivity(ProcessTrafficTracker.Instance);
            }
        }

        if (BlockedGrid.ItemsSource is IEnumerable<AppRuleRow> blockedRows)
        {
            foreach (var row in blockedRows)
            {
                row.UpdateActivity(ProcessTrafficTracker.Instance);
            }
        }
    }

    private static Dictionary<string, string> LastRemoteByApp()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = BlockedLog.LogPath;
            if (!File.Exists(path)) return map;
            foreach (var line in File.ReadLines(path))
            {
                try
                {
                    var parts = line.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length < 5) continue;
                    var ip = NetworkEndpoint.ExtractAddress(parts[4]);
                    if (string.IsNullOrEmpty(ip)) continue;
                    string key;
                    try { key = AppRule.Normalize(parts[3]); }
                    catch { key = parts[3]; }

                    var newGeo = GeoCountry.Lookup(ip);
                    if (map.TryGetValue(key, out var existingIp))
                    {
                        var existingGeo = GeoCountry.Lookup(existingIp);
                        if (existingGeo.HasCountry && !newGeo.HasCountry)
                            continue;
                    }

                    map[key] = ip;
                }
                catch { }
            }
        }
        catch { }
        return map;
    }

    private void UpdateStatus()
    {
        var on = _svc.IsMasterOn;
        MasterToggle.IsChecked = on;
        MasterLabel.Text = on ? Strings.T("ProtectionActive") : Strings.T("ProtectionDisabled");
        StatusDot.Fill = on ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
        ModeBox.IsEnabled = on;
        TrafficIndicator.SetActive(on, _svc.Mode);
        var real = _svc.Wfp.IsRealBlock ? " • WFP Real" : " • Simulado";
        if (!on) StatusText.Text = Strings.T("StatusDisabled") + real;
        else
        {
            var statusKey = _svc.Mode switch
            {
                FirewallMode.BlockAll => "StatusEnabledBlock",
                FirewallMode.Killswitch => "StatusEnabledKillswitch",
                _ => "StatusEnabledAsk"
            };
            StatusText.Text = Strings.T(statusKey) + real;
        }
    }

    private void MasterToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (MasterToggle.IsChecked == true)
        {
            var m = ModeBox.SelectedIndex switch
            {
                1 => FirewallMode.BlockAll,
                2 => FirewallMode.Killswitch,
                _ => FirewallMode.Ask
            };
            _svc.Enable(m);
            _notifications.PurgeObsoleteProtectionOffNotifications();
            if (App.Settings.ToastProtectionEventsEnabled)
            {
                AppInfoToast.ShowToast(Strings.T("NotifProtectionOn"), Strings.T("NotifProtectionOnDesc"), appPath: null, ToastBadgeType.Success);
            }
        }
        else
        {
            _svc.Disable();
            _notifications.Add(AppNotificationKind.ProtectionOff);
            if (App.Settings.ToastProtectionEventsEnabled)
            {
                AppInfoToast.ShowToast(Strings.T("NotifProtectionOffTitle"), Strings.T("NotifProtectionOffDesc"), appPath: null, ToastBadgeType.Warning);
            }
        }
        App.Settings.FirewallEnabled = _svc.IsMasterOn;
        App.Settings.FirewallMode = (int)_svc.Mode;
        App.Settings.Save();
        UpdateStatus();
    }

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_svc.IsMasterOn) return;
        var m = ModeBox.SelectedIndex switch
        {
            1 => FirewallMode.BlockAll,
            2 => FirewallMode.Killswitch,
            _ => FirewallMode.Ask
        };
        _svc.SetMode(m);
        App.Settings.FirewallMode = (int)m;
        App.Settings.Save();
        UpdateStatus();
    }

    private void RulesColumnHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement header || header.Tag is not string sortBy)
            return;

        if (_sortBy == sortBy)
            _sortAsc = !_sortAsc;
        else
        {
            _sortBy = sortBy;
            _sortAsc = true;
        }

        RefreshRules(SearchBox.Text);
        e.Handled = true;
    }

    private static void ExecuteSearchOnline(AppRule r)
    {
        var exeName = Path.GetFileName(r.AppPath);
        var displayName = !string.IsNullOrWhiteSpace(r.DisplayName) ? r.DisplayName.Trim() : Path.GetFileNameWithoutExtension(r.AppPath);
        string query;
        if (string.Equals(exeName, displayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(exeName), displayName, StringComparison.OrdinalIgnoreCase))
        {
            query = $"{exeName} {Path.GetFileNameWithoutExtension(exeName)}";
        }
        else
        {
            query = $"{exeName} {displayName}";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void QuickSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        var r = btn.Tag as AppRule ?? (btn.Tag as AppRuleRow)?.Rule;
        if (r != null) ExecuteSearchOnline(r);
    }

    private void OpenEditRule(AppRule rule, GeoResult? geo = null)
    {
        if (geo == null)
        {
            var last = LastRemoteByApp();
            foreach (var entry in _lastRemoteByApp)
                last[entry.Key] = entry.Value;
            string? ip = null;
            try { last.TryGetValue(AppRule.Normalize(rule.AppPath), out ip); } catch { }
            ip ??= last.GetValueOrDefault(rule.AppPath);
            geo = GeoCountry.Lookup(ip);
        }

        var dlg = new EditRuleDialog(rule, geo) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _svc.UpdateRule(rule.AppPath, dlg.InboundVerdict, dlg.OutboundVerdict);
            RefreshRules(SearchBox.Text);
        }
    }

    private void QuickEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        var row = btn.Tag as AppRuleRow;
        var r = row?.Rule ?? btn.Tag as AppRule;
        if (r != null) OpenEditRule(r, row?.Geo);
    }

    private void DirectionBadge_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement elem) return;
        var row = elem.Tag as AppRuleRow;
        var r = row?.Rule ?? elem.Tag as AppRule;
        if (r != null)
        {
            e.Handled = true;
            OpenEditRule(r, row?.Geo);
        }
    }

    private void RulesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem)
        {
            var row = elem.DataContext as AppRuleRow;
            var r = row?.Rule ?? elem.DataContext as AppRule;
            if (r != null)
            {
                e.Handled = true;
                OpenEditRule(r, row?.Geo);
            }
        }
    }

    private void ContextEditRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var row = item.DataContext as AppRuleRow ?? (item.Tag as AppRuleRow);
        var r = row?.Rule ?? item.DataContext as AppRule ?? (item.Tag as AppRule);
        if (r == null && AllowedGrid.SelectedItem is AppRuleRow selRow) { row = selRow; r = selRow.Rule; }
        if (r == null && BlockedGrid.SelectedItem is AppRuleRow selBlockRow) { row = selBlockRow; r = selBlockRow.Rule; }
        if (r != null) OpenEditRule(r, row?.Geo);
    }

    private void ContextSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var r = item.DataContext as AppRule ?? (item.DataContext as AppRuleRow)?.Rule ?? (item.Tag as AppRule) ?? (item.Tag as AppRuleRow)?.Rule;
        if (r == null && (AllowedGrid.SelectedItem as AppRuleRow)?.Rule is { } selRow) r = selRow;
        if (r == null && (BlockedGrid.SelectedItem as AppRuleRow)?.Rule is { } selBlockRow) r = selBlockRow;
        if (r != null) ExecuteSearchOnline(r);
    }

    private void ContextCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var r = item.DataContext as AppRule ?? (item.DataContext as AppRuleRow)?.Rule ?? (item.Tag as AppRule) ?? (item.Tag as AppRuleRow)?.Rule;
        if (r == null && (AllowedGrid.SelectedItem as AppRuleRow)?.Rule is { } selRow) r = selRow;
        if (r == null && (BlockedGrid.SelectedItem as AppRuleRow)?.Rule is { } selBlockRow) r = selBlockRow;
        var path = r?.AppPath;
        if (string.IsNullOrEmpty(path)) return;
        try { System.Windows.Clipboard.SetText(path); } catch { }
    }

    private void ContextFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var r = item.DataContext as AppRule ?? (item.DataContext as AppRuleRow)?.Rule ?? (item.Tag as AppRule) ?? (item.Tag as AppRuleRow)?.Rule;
        if (r == null && (AllowedGrid.SelectedItem as AppRuleRow)?.Rule is { } selRow) r = selRow;
        if (r == null && (BlockedGrid.SelectedItem as AppRuleRow)?.Rule is { } selBlockRow) r = selBlockRow;
        var path = r?.AppPath;
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void ContextRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var r = item.DataContext as AppRule ?? (item.DataContext as AppRuleRow)?.Rule ?? (item.Tag as AppRule) ?? (item.Tag as AppRuleRow)?.Rule;
        if (r == null && (AllowedGrid.SelectedItem as AppRuleRow)?.Rule is { } selRow) r = selRow;
        if (r == null && (BlockedGrid.SelectedItem as AppRuleRow)?.Rule is { } selBlockRow) r = selBlockRow;
        if (r == null) return;
        var dlg = new ConfirmDialog(r.DisplayName, r.AppPath) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _svc.RemoveRule(r.AppPath);
            RefreshRules(SearchBox.Text);
        }
    }

    private void QuickCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        var r = btn.Tag as AppRule ?? (btn.Tag as AppRuleRow)?.Rule;
        var path = r?.AppPath;
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Windows.Clipboard.SetText(path);
        }
        catch { }
    }

    private void QuickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        var r = btn.Tag as AppRule ?? (btn.Tag as AppRuleRow)?.Rule;
        var path = r?.AppPath;
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void QuickRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn) return;
        var r = btn.Tag as AppRule ?? (btn.Tag as AppRuleRow)?.Rule;
        if (r == null) return;
        var dlg = new ConfirmDialog(r.DisplayName, r.AppPath) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _svc.RemoveRule(r.AppPath);
            RefreshRules(SearchBox.Text);
        }
    }

    private void RuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox cb)
        {
            var r = cb.DataContext as AppRule ?? (cb.DataContext as AppRuleRow)?.Rule;
            if (r == null) return;
            var allow = cb.IsChecked == true;
            _svc.SetVerdict(r.AppPath, allow ? Verdict.Allow : Verdict.Block, true, null);
            RefreshRules(SearchBox.Text);
        }
    }

    public void OpenSettings()
    {
        var w = new SettingsWindow(App.Settings) { Owner = this };
        w.ShowDialog();
        RefreshLanguage();
        UpdateStatus();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutWindow(App.Settings) { Owner = this };
        dlg.ShowDialog();
    }
    
    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new LogViewerDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenStats_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new StatisticsDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGrid grid && grid.SelectedItem != null)
        {
            if (grid == AllowedGrid)
                BlockedGrid.UnselectAll();
            else if (grid == BlockedGrid)
                AllowedGrid.UnselectAll();
        }
    }

    private void RulesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0)
        {
            DismissOpenToolTips();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        bool hasText = !string.IsNullOrEmpty(text);

        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        if (SearchClearBtn != null)
            SearchClearBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        if (SearchHotkeyHint != null)
            SearchHotkeyHint.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;

        // Skip heavy search on 1 single char to avoid freezes on large rule sets; search on empty or >= 2 chars
        if (string.IsNullOrEmpty(text))
        {
            RefreshRules("");
        }
        else if (text.Length >= 2)
        {
            RefreshRules(text);
        }
    }

    private void SearchClearBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void SelectAndFocusRow(System.Windows.Controls.DataGrid grid, int index)
    {
        if (grid == null || grid.Items.Count == 0) return;
        int targetIdx = Math.Clamp(index, 0, grid.Items.Count - 1);

        grid.Focus();
        grid.SelectedIndex = targetIdx;
        grid.CurrentItem = grid.Items[targetIdx];
        grid.ScrollIntoView(grid.Items[targetIdx]);

        grid.UpdateLayout();
        if (grid.ItemContainerGenerator.ContainerFromIndex(targetIdx) is DataGridRow row)
        {
            row.Focus();
            row.IsSelected = true;
        }
    }

    private void ToggleRuleAndAdvanceSelection(System.Windows.Controls.DataGrid grid)
    {
        if (grid == null || grid.Items.Count == 0) return;
        var rowItem = grid.SelectedItem ?? grid.CurrentItem;
        var r = rowItem as AppRule ?? (rowItem as AppRuleRow)?.Rule;
        if (r == null) return;

        int currentIdx = grid.SelectedIndex >= 0 ? grid.SelectedIndex : 0;
        bool isAllowed = (grid == AllowedGrid);
        var newVerdict = r.Verdict == Verdict.Allow ? Verdict.Block : Verdict.Allow;

        _svc.SetVerdict(r.AppPath, newVerdict, true, null);

        _refreshRulesDebounceTimer?.Stop();
        ExecuteRefreshRules(SearchBox?.Text);

        var targetGrid = isAllowed ? AllowedGrid : BlockedGrid;
        var otherGrid = isAllowed ? BlockedGrid : AllowedGrid;

        if (targetGrid.Items.Count > 0)
        {
            int nextIdx = Math.Clamp(currentIdx, 0, targetGrid.Items.Count - 1);
            SelectAndFocusRow(targetGrid, nextIdx);
        }
        else if (otherGrid.Items.Count > 0)
        {
            SelectAndFocusRow(otherGrid, 0);
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            if (_refreshRulesDebounceTimer != null && _refreshRulesDebounceTimer.IsEnabled)
            {
                _refreshRulesDebounceTimer.Stop();
                ExecuteRefreshRules(SearchBox.Text);
            }

            var targetGrid = AllowedGrid.Items.Count > 0 ? AllowedGrid : (BlockedGrid.Items.Count > 0 ? BlockedGrid : null);
            if (targetGrid != null && targetGrid.Items.Count > 0)
            {
                SelectAndFocusRow(targetGrid, 0);
            }
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = "";
                e.Handled = true;
            }
        }
    }

    private void DataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        if (e.Key == System.Windows.Input.Key.Space)
        {
            e.Handled = true;
            ToggleRuleAndAdvanceSelection(grid);
        }
        else if (e.Key == System.Windows.Input.Key.Down)
        {
            e.Handled = true;
            if (grid.SelectedIndex < grid.Items.Count - 1)
            {
                SelectAndFocusRow(grid, grid.SelectedIndex + 1);
            }
            else if (grid == AllowedGrid && BlockedGrid.Items.Count > 0)
            {
                SelectAndFocusRow(BlockedGrid, 0);
            }
        }
        else if (e.Key == System.Windows.Input.Key.Up)
        {
            e.Handled = true;
            if (grid.SelectedIndex > 0)
            {
                SelectAndFocusRow(grid, grid.SelectedIndex - 1);
            }
            else if (grid == BlockedGrid && AllowedGrid.Items.Count > 0)
            {
                SelectAndFocusRow(AllowedGrid, AllowedGrid.Items.Count - 1);
            }
            else if (grid == AllowedGrid)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                AllowedGrid.UnselectAll();
            }
        }
        else if (e.Key == System.Windows.Input.Key.Tab)
        {
            e.Handled = true;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if (grid == BlockedGrid && AllowedGrid.Items.Count > 0)
                {
                    SelectAndFocusRow(AllowedGrid, Math.Min(grid.SelectedIndex >= 0 ? grid.SelectedIndex : 0, AllowedGrid.Items.Count - 1));
                }
                else
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    grid.UnselectAll();
                }
            }
            else
            {
                if (grid == AllowedGrid && BlockedGrid.Items.Count > 0)
                {
                    SelectAndFocusRow(BlockedGrid, Math.Min(grid.SelectedIndex >= 0 ? grid.SelectedIndex : 0, BlockedGrid.Items.Count - 1));
                }
                else
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    grid.UnselectAll();
                }
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

        if (e.Key == System.Windows.Input.Key.Home)
        {
            DismissOpenToolTips();
            RulesScrollViewer.ScrollToHome();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.End)
        {
            DismissOpenToolTips();
            RulesScrollViewer.ScrollToEnd();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.PageUp)
        {
            DismissOpenToolTips();
            RulesScrollViewer.PageUp();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.PageDown)
        {
            DismissOpenToolTips();
            RulesScrollViewer.PageDown();
            e.Handled = true;
        }
    }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        DismissOpenToolTips();
        RulesScrollViewer.ScrollToVerticalOffset(RulesScrollViewer.VerticalOffset - (e.Delta / 2.0));
        e.Handled = true;
    }

    private static readonly DependencyPropertyKey? ToolTipIsOpenKey =
        typeof(ToolTipService).GetField("IsOpenPropertyKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as DependencyPropertyKey;

    private void DismissOpenToolTips()
    {
        try
        {
            if (ToolTipIsOpenKey == null) return;
            var cur = Mouse.DirectlyOver as DependencyObject;
            while (cur != null)
            {
                if (cur is UIElement elem)
                {
                    elem.SetValue(ToolTipIsOpenKey, false);
                }
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            }
        }
        catch { }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

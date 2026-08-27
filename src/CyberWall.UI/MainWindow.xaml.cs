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
        Closed += (_, _) =>
        {
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
        _loading = false;
    }

    private async void CheckForUpdatesOnStartup()
    {
        if (!App.Settings.AutoCheckForUpdates) return;
        try
        {
            await Task.Delay(3000);
            var result = await UpdateService.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                Dispatcher.Invoke(() =>
                {
                    _notifications.Add(AppNotificationKind.UpdateAvailable, detail: result.LatestVersionLabel);
                    var choice = ConfirmDialog.Show(
                        this,
                        Strings.T("UpdateAvailable", result.LatestVersionLabel),
                        $"{result.StatusMessage}\n\n{Strings.T("Current")} {UpdateService.GetCurrentVersionLabel()}\n{Strings.T("Latest")} {result.LatestVersionLabel}\n\n{Strings.T("UpdatePrompt")}",
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
        ModeLbl.Text = Strings.T("Mode");
        SearchPlaceholder.Text = Strings.T("SearchPlaceholder");
        ViewLogBtnText.Text = Strings.T("ViewLog");
        TrafficIndicator.RefreshLanguage();
        _tray?.RefreshLanguage();

        var progHdr = Strings.T("Program") + (_sortBy == "DisplayName" ? (_sortAsc ? " ▾" : " ▴") : "");
        var pathHdr = Strings.T("Path") + (_sortBy == "AppPath" ? (_sortAsc ? " ▾" : " ▴") : "");
        var actHdr = Strings.T("Action");
        var dirHdr = Strings.T("Direction");
        var stateHdr = Strings.T("State");
        var countryHdr = Strings.T("Country");

        StateHeaderText.Text = stateHdr;
        ProgramHeaderText.Text = progHdr;
        PathHeaderText.Text = pathHdr;
        ActionHeaderText.Text = actHdr;
        CountryHeaderText.Text = countryHdr;
        DirectionHeaderText.Text = dirHdr;

        AllowColState.Header = stateHdr;
        AllowColProg.Header = progHdr;
        AllowColPath.Header = pathHdr;
        AllowColAction.Header = actHdr;
        AllowColCountry.Header = countryHdr;
        AllowColDir.Header = dirHdr;

        BlockColState.Header = stateHdr;
        BlockColProg.Header = progHdr;
        BlockColPath.Header = pathHdr;
        BlockColAction.Header = actHdr;
        BlockColCountry.Header = countryHdr;
        BlockColDir.Header = dirHdr;

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
        RememberLastRemote(ev);
        PromptManager.Instance.Enqueue(ev);
    }

    private void OnUnknownBlocked(ConnectionEvent ev)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RememberLastRemote(ev);
            _notifications.Add(AppNotificationKind.SilentBlock, ev.AppPath, ev.DisplayName,
                string.IsNullOrEmpty(ev.RemoteAddress) ? null : $"{ev.RemoteAddress}:{ev.RemotePort}");
        });
    }

    internal void RecordAutoBlock(ConnectionEvent ev)
    {
        _notifications.Add(AppNotificationKind.AutoBlocked, ev.AppPath, ev.DisplayName,
            string.IsNullOrEmpty(ev.RemoteAddress) ? null : $"{ev.RemoteAddress}:{ev.RemotePort}");
        _tray?.NotifyAutoBlock(ev.DisplayName);
        AutoBlockToast.ShowToast(ev, () =>
        {
            _svc.SetVerdict(ev.AppPath, Verdict.Allow, true, ev);
            _notifications.MarkRelatedRead(AppNotificationKind.AutoBlocked, ev.AppPath);
            RefreshRules(SearchBox.Text);
        });
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
                },
                OpenUpdateFromNotification)
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
        Func<AppRule, object> key = _sortBy == "AppPath" ? r => r.AppPath : r => r.DisplayName;
        var blocked = q.Where(r => r.Verdict == Verdict.Block);
        var allowed = q.Where(r => r.Verdict == Verdict.Allow);
        blocked = _sortAsc ? blocked.OrderBy(key) : blocked.OrderByDescending(key);
        allowed = _sortAsc ? allowed.OrderBy(key) : allowed.OrderByDescending(key);
        BlockedGrid.ItemsSource = blocked.Select(r => ToRow(r, last)).ToList();
        AllowedGrid.ItemsSource = allowed.Select(r => ToRow(r, last)).ToList();
        RefreshLanguage();
    }

    private void RememberLastRemote(ConnectionEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.RemoteAddress) ||
            !System.Net.IPAddress.TryParse(ev.RemoteAddress, out _))
            return;

        try
        {
            _lastRemoteByApp[AppRule.Normalize(ev.AppPath)] = ev.RemoteAddress;
        }
        catch
        {
            _lastRemoteByApp[ev.AppPath] = ev.RemoteAddress;
        }
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
        return new AppRuleRow { Rule = rule, Geo = GeoCountry.Lookup(ip) };
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
            _notifications.MarkRelatedRead(AppNotificationKind.ProtectionOff, null);
        }
        else
        {
            _svc.Disable();
            _notifications.Add(AppNotificationKind.ProtectionOff);
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

    private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGrid grid)
            grid.UnselectAll();
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

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Enter)
        {
            try
            {
                if (AllowedGrid.Items.Count > 0)
                {
                    AllowedGrid.Focus();
                    if (AllowedGrid.Items[0] != null)
                    {
                        AllowedGrid.CurrentItem = AllowedGrid.Items[0];
                    }
                    e.Handled = true;
                }
                else if (BlockedGrid.Items.Count > 0)
                {
                    BlockedGrid.Focus();
                    if (BlockedGrid.Items[0] != null)
                    {
                        BlockedGrid.CurrentItem = BlockedGrid.Items[0];
                    }
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search navigation error: {ex}");
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
        if (e.Key == System.Windows.Input.Key.Space)
        {
            if (sender is System.Windows.Controls.DataGrid grid)
            {
                var rowItem = grid.SelectedItem ?? grid.CurrentItem;
                var r = rowItem as AppRule ?? (rowItem as AppRuleRow)?.Rule;
                if (r != null)
                {
                    var newVerdict = r.Verdict == Verdict.Allow ? Verdict.Block : Verdict.Allow;
                    _svc.SetVerdict(r.AppPath, newVerdict, true, null);
                    RefreshRules(SearchBox.Text);
                    e.Handled = true;
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

        if (e.Key == System.Windows.Input.Key.Space)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var dataGrid = FindAncestor<System.Windows.Controls.DataGrid>(focused);
            if (dataGrid != null)
            {
                var rowItem = dataGrid.SelectedItem ?? dataGrid.CurrentItem;
                var r = rowItem as AppRule ?? (rowItem as AppRuleRow)?.Rule;
                if (r != null)
                {
                    var newVerdict = r.Verdict == Verdict.Allow ? Verdict.Block : Verdict.Allow;
                    _svc.SetVerdict(r.AppPath, newVerdict, true, null);
                    RefreshRules(SearchBox.Text);
                    e.Handled = true;
                    return;
                }
            }
        }

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

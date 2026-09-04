using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Services;
using UserControl = System.Windows.Controls.UserControl;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace CyberWall.UI.Views;

public partial class StatisticsView : UserControl
{
    private readonly List<ParsedLogEvent> _allEvents = new();
    private bool _isInitializing = true;
    private bool _isActive;
    private StatVerdictFilter _verdictFilter = StatVerdictFilter.All;
    private readonly DispatcherTimer _autoRefreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private long _lastKnownLogLength = -1;

    public StatisticsView()
    {
        InitializeComponent();
        GeoCountry.Updated += OnGeoUpdated;
        PopulatePeriods();
        RefreshLanguage();
        _isInitializing = false;

        _autoRefreshTimer.Tick += (_, _) => AutoRefreshTick();
    }

    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        _autoRefreshTimer.Start();
        LoadAndComputeStats();
    }

    public void Deactivate()
    {
        _isActive = false;
        _autoRefreshTimer.Stop();
    }

    private void AutoRefreshTick()
    {
        if (!_isActive) return;
        UpdateSessionVolume();
        try
        {
            var path = BlockedLog.LogPath;
            if (File.Exists(path))
            {
                var len = new FileInfo(path).Length;
                if (len != _lastKnownLogLength)
                {
                    LoadAndComputeStats(silent: true);
                }
            }
        }
        catch { }
    }

    private void UpdateSessionVolume()
    {
        try
        {
            var snapshot = NetworkSpeedService.Instance.CurrentSnapshot;
            DataVolumeVal.Text = $"↓ {NetworkSpeedService.FormatBytes(snapshot.TotalBytesReceived)}";
            DataVolumeDesc.Text = $"↑ {NetworkSpeedService.FormatBytes(snapshot.TotalBytesSent)} · {Strings.T("StatsSessionData")}";
        }
        catch { }
    }

    private void PopulatePeriods()
    {
        PeriodCombo.Items.Clear();
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriodToday"), Tag = StatPeriod.Today });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod24h"), Tag = StatPeriod.Last24Hours });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod7d"), Tag = StatPeriod.Last7Days });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod30d"), Tag = StatPeriod.Last30Days });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriodAll"), Tag = StatPeriod.AllTime });

        PeriodCombo.SelectedIndex = 1; // Default to Last 24 Hours
    }

    public void RefreshLanguage()
    {
        PeriodLabel.Text = Strings.T("StatsPeriodLabel");
        RefreshBtnText.Text = Strings.T("Refresh");

        FilterAllRadio.Content = Strings.T("StatsFilterAll");
        FilterBlockedRadio.Content = Strings.T("StatsFilterBlocked");
        FilterAllowedRadio.Content = Strings.T("StatsFilterAllowed");

        CardTotalTitle.Text = Strings.T("StatsTotalEvents");
        CardTotalDesc.Text = Strings.T("StatsEventsProcessed");
        CardBlockedTitle.Text = Strings.T("StatsBlocked");
        CardBlockedDesc.Text = Strings.T("StatsBlockedDesc");
        CardAllowedTitle.Text = Strings.T("StatsAllowed");
        CardAllowedDesc.Text = Strings.T("StatsAllowedDesc");
        CardVolumeTitle.Text = Strings.T("StatsDataVolume");
        CardScopeTitle.Text = Strings.T("StatsGlobalScope");

        TopAppsTitle.Text = Strings.T("StatsTopApps");
        TrafficDirTitle.Text = Strings.T("StatsTrafficDirection");
        OutboundLbl.Text = Strings.T("StatsOutbound") + " (Outbound)";
        InboundLbl.Text = Strings.T("StatsInbound") + " (Inbound)";

        TopCountriesTitle.Text = Strings.T("StatsTopCountries");
        ClearStatsBtnText.Text = Strings.T("StatsResetBtn");
        EmptyMsg.Text = Strings.T("StatsNoDataPeriod");

        var curIdx = PeriodCombo.SelectedIndex;
        PopulatePeriods();
        PeriodCombo.SelectedIndex = curIdx >= 0 ? curIdx : 1;
    }

    private void OnGeoUpdated()
    {
        if (!_isActive) return;
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var ev in _allEvents)
            {
                if (!ev.Geo.HasCountry && !string.IsNullOrWhiteSpace(ev.RemoteAddress))
                {
                    ev.Geo = GeoCountry.Lookup(ev.RemoteAddress);
                }
            }
            ComputeDashboard();
        });
    }

    private void LoadAndComputeStats(bool silent = false)
    {
        if (_isInitializing) return;

        _allEvents.Clear();
        var path = BlockedLog.LogPath;
        if (File.Exists(path))
        {
            try
            {
                var fi = new FileInfo(path);
                _lastKnownLogLength = fi.Length;

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, System.Text.Encoding.UTF8);

                while (sr.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 5)
                    {
                        var tsStr = parts[0];
                        if (!DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            if (!DateTime.TryParse(tsStr, out dt))
                                dt = DateTime.Now;
                        }

                        var verd = parts[1].Equals("Block", StringComparison.OrdinalIgnoreCase) ? Verdict.Block : Verdict.Allow;
                        var dir = parts[2].Equals("Inbound", StringComparison.OrdinalIgnoreCase) ? Direction.Inbound : Direction.Outbound;
                        var app = parts[3];
                        var ep = parts[4];
                        var pid = parts.Length > 5 ? parts[5] : "";

                        var addr = NetworkEndpoint.ExtractAddress(ep) ?? "";
                        int port = 0;
                        var colonIdx = ep.LastIndexOf(':');
                        if (colonIdx >= 0 && int.TryParse(ep[(colonIdx + 1)..], out var p))
                            port = p;
                        var geo = GeoCountry.Lookup(addr);

                        _allEvents.Add(new ParsedLogEvent
                        {
                            Timestamp = dt,
                            Verdict = verd,
                            Direction = dir,
                            AppPath = app,
                            DisplayName = Path.GetFileName(app),
                            RemoteEndpoint = ep,
                            RemoteAddress = addr,
                            RemotePort = port,
                            ProcessId = pid,
                            Geo = geo
                        });
                    }
                }
            }
            catch { }
        }

        UpdateSessionVolume();
        ComputeDashboard();
    }

    private void ComputeDashboard()
    {
        var selectedItem = PeriodCombo.SelectedItem as ComboBoxItem;
        var period = (selectedItem?.Tag as StatPeriod?) ?? StatPeriod.Last24Hours;

        var now = DateTime.Now;
        DateTime cutoff = period switch
        {
            StatPeriod.Today => now.Date,
            StatPeriod.Last24Hours => now.AddHours(-24),
            StatPeriod.Last7Days => now.AddDays(-7),
            StatPeriod.Last30Days => now.AddDays(-30),
            StatPeriod.AllTime => DateTime.MinValue,
            _ => now.AddHours(-24)
        };

        var filtered = _allEvents.Where(e => e.Timestamp >= cutoff);

        if (_verdictFilter == StatVerdictFilter.BlockedOnly)
            filtered = filtered.Where(e => e.Verdict == Verdict.Block);
        else if (_verdictFilter == StatVerdictFilter.AllowedOnly)
            filtered = filtered.Where(e => e.Verdict == Verdict.Allow);

        var list = filtered.ToList();

        if (list.Count == 0)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            TotalEventsVal.Text = "0";
            BlockedEventsVal.Text = "0";
            AllowedEventsVal.Text = "0";
            return;
        }

        DashboardView.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;

        int totalCount = list.Count;
        int blockedCount = list.Count(e => e.Verdict == Verdict.Block);
        int allowedCount = totalCount - blockedCount;

        double blockedPct = totalCount > 0 ? (blockedCount * 100.0 / totalCount) : 0;
        double allowedPct = totalCount > 0 ? (allowedCount * 100.0 / totalCount) : 0;

        TotalEventsVal.Text = totalCount.ToString("N0", CultureInfo.CurrentCulture);
        BlockedEventsVal.Text = blockedCount.ToString("N0", CultureInfo.CurrentCulture);
        BlockedPctVal.Text = $" ({blockedPct:F0}%)";
        AllowedEventsVal.Text = allowedCount.ToString("N0", CultureInfo.CurrentCulture);
        AllowedPctVal.Text = $" ({allowedPct:F0}%)";

        int uniqueApps = list.Select(e => e.AppPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int uniqueIps = list.Select(e => e.RemoteAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int uniqueCountries = list.Where(e => e.Geo.HasCountry).Select(e => e.Geo.Iso2).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        ScopeVal.Text = Strings.T("StatsCountriesCount", uniqueCountries);
        CardScopeDesc.Text = Strings.T("StatsScopeDetail", uniqueApps, uniqueIps);

        // Direction stats
        int outbound = list.Count(e => e.Direction == Direction.Outbound);
        int inbound = totalCount - outbound;
        double outPct = totalCount > 0 ? (outbound * 100.0 / totalCount) : 50;
        double inPct = totalCount > 0 ? (inbound * 100.0 / totalCount) : 50;

        OutboundCountVal.Text = outbound.ToString("N0", CultureInfo.CurrentCulture);
        OutboundPctVal.Text = $" ({outPct:F0}%)";
        InboundCountVal.Text = inbound.ToString("N0", CultureInfo.CurrentCulture);
        InboundPctVal.Text = $" ({inPct:F0}%)";

        ColOutboundBar.Width = new GridLength(Math.Max(1, outPct), GridUnitType.Star);
        ColInboundBar.Width = new GridLength(Math.Max(1, inPct), GridUnitType.Star);

        // Top Apps
        var topApps = list
            .GroupBy(e => string.IsNullOrWhiteSpace(e.AppPath) ? e.DisplayName : e.AppPath, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var count = g.Count();
                var pct = (count * 100.0) / totalCount;
                return new StatItem
                {
                    Name = string.IsNullOrWhiteSpace(first.DisplayName) ? "Unknown" : first.DisplayName,
                    IconPath = first.AppPath,
                    Count = count,
                    Percentage = pct,
                    TooltipText = first.AppPath
                };
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        TopAppsList.ItemsSource = topApps;

        // Top Countries
        var topCountries = list
            .Where(e => e.Geo.HasCountry)
            .GroupBy(e => e.Geo.Iso2, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var count = g.Count();
                var pct = (count * 100.0) / totalCount;
                return new StatItem
                {
                    Name = CountryDisplay.Label(first.Geo),
                    CountryCode = first.Geo.Iso2 ?? "",
                    HasCountry = true,
                    CountryLabel = CountryDisplay.Label(first.Geo),
                    Count = count,
                    Percentage = pct
                };
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        TopCountriesList.ItemsSource = topCountries;
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
            ComputeDashboard();
    }

    private void FilterRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (FilterBlockedRadio.IsChecked == true)
            _verdictFilter = StatVerdictFilter.BlockedOnly;
        else if (FilterAllowedRadio.IsChecked == true)
            _verdictFilter = StatVerdictFilter.AllowedOnly;
        else
            _verdictFilter = StatVerdictFilter.All;

        if (!_isInitializing)
            ComputeDashboard();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadAndComputeStats();
    }

    private void ClearStats_Click(object sender, RoutedEventArgs e)
    {
        var parent = Window.GetWindow(this);
        var res = ConfirmDialog.Show(
            parent,
            Strings.T("StatsResetTitle"),
            Strings.T("StatsResetConfirm"),
            Strings.T("StatsResetBtn"),
            Strings.T("Cancel"));

        if (res)
        {
            BlockedLog.Clear();
            LoadAndComputeStats();
        }
    }
}

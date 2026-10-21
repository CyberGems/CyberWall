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
    private IReadOnlyList<ConnectionLogEntry> _entries = Array.Empty<ConnectionLogEntry>();
    private bool _isInitializing = true;
    private bool _isActive;
    private StatVerdictFilter _verdictFilter = StatVerdictFilter.All;
    private readonly DispatcherTimer _autoRefreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };

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
            // The reader returns the same snapshot when nothing was appended, so the
            // dashboard is only recomputed when there is something new.
            var entries = ConnectionLogReader.Refresh();
            if (ReferenceEquals(entries, _entries)) return;

            _entries = entries;
            ConnectionLogGeo.Resolve(_entries);
            ComputeDashboard();
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
            ConnectionLogGeo.Resolve(_entries, force: true);
            ComputeDashboard();
        });
    }

    public void LoadAndComputeStats(bool silent = false)
    {
        if (_isInitializing) return;

        _entries = ConnectionLogReader.Refresh();
        ConnectionLogGeo.Resolve(_entries);

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

        // Everything is counted in a single pass over the entries.
        int totalCount = 0;
        int blockedCount = 0;
        int outboundCount = 0;
        var uniqueIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appCounts = new Dictionary<string, (string DisplayName, string AppPath, int Count)>(StringComparer.OrdinalIgnoreCase);
        var countryCounts = new Dictionary<string, (GeoResult Geo, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in _entries)
        {
            if (e.Timestamp < cutoff) continue;
            if (_verdictFilter == StatVerdictFilter.BlockedOnly && e.Verdict != Verdict.Block) continue;
            if (_verdictFilter == StatVerdictFilter.AllowedOnly && e.Verdict != Verdict.Allow) continue;

            totalCount++;
            if (e.Verdict == Verdict.Block) blockedCount++;
            if (e.Direction == Direction.Outbound) outboundCount++;
            if (!string.IsNullOrEmpty(e.RemoteAddress)) uniqueIps.Add(e.RemoteAddress);

            var appKey = string.IsNullOrWhiteSpace(e.AppPath) ? e.DisplayName : e.AppPath;
            if (appCounts.TryGetValue(appKey, out var appInfo))
                appCounts[appKey] = (appInfo.DisplayName, appInfo.AppPath, appInfo.Count + 1);
            else
                appCounts[appKey] = (e.DisplayName, e.AppPath, 1);

            if (e.Geo.HasCountry && e.Geo.Iso2 is { Length: 2 } iso)
            {
                if (countryCounts.TryGetValue(iso, out var countryInfo))
                    countryCounts[iso] = (countryInfo.Geo, countryInfo.Count + 1);
                else
                    countryCounts[iso] = (e.Geo, 1);
            }
        }

        if (totalCount == 0)
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

        int allowedCount = totalCount - blockedCount;

        double blockedPct = blockedCount * 100.0 / totalCount;
        double allowedPct = allowedCount * 100.0 / totalCount;

        TotalEventsVal.Text = totalCount.ToString("N0", CultureInfo.CurrentCulture);
        BlockedEventsVal.Text = blockedCount.ToString("N0", CultureInfo.CurrentCulture);
        BlockedPctVal.Text = $" ({blockedPct:F0}%)";
        AllowedEventsVal.Text = allowedCount.ToString("N0", CultureInfo.CurrentCulture);
        AllowedPctVal.Text = $" ({allowedPct:F0}%)";

        int uniqueApps = appCounts.Count;
        int uniqueCountries = countryCounts.Count;

        ScopeVal.Text = Strings.T("StatsCountriesCount", uniqueCountries);
        CardScopeDesc.Text = Strings.T("StatsScopeDetail", uniqueApps, uniqueIps.Count);

        // Direction stats
        int inbound = totalCount - outboundCount;
        double outPct = outboundCount * 100.0 / totalCount;
        double inPct = inbound * 100.0 / totalCount;

        OutboundCountVal.Text = outboundCount.ToString("N0", CultureInfo.CurrentCulture);
        OutboundPctVal.Text = $" ({outPct:F0}%)";
        InboundCountVal.Text = inbound.ToString("N0", CultureInfo.CurrentCulture);
        InboundPctVal.Text = $" ({inPct:F0}%)";

        ColOutboundBar.Width = new GridLength(Math.Max(1, outPct), GridUnitType.Star);
        ColInboundBar.Width = new GridLength(Math.Max(1, inPct), GridUnitType.Star);

        // Top Apps
        var topApps = appCounts
            .Select(kv => new StatItem
            {
                Name = string.IsNullOrWhiteSpace(kv.Value.DisplayName) ? "Unknown" : kv.Value.DisplayName,
                IconPath = kv.Value.AppPath,
                Count = kv.Value.Count,
                Percentage = (kv.Value.Count * 100.0) / totalCount,
                TooltipText = kv.Value.AppPath
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        TopAppsList.ItemsSource = topApps;

        // Top Countries
        var topCountries = countryCounts
            .Select(kv => new StatItem
            {
                Name = CountryDisplay.Label(kv.Value.Geo),
                CountryCode = kv.Value.Geo.Iso2 ?? "",
                HasCountry = true,
                CountryLabel = CountryDisplay.Label(kv.Value.Geo),
                Count = kv.Value.Count,
                Percentage = (kv.Value.Count * 100.0) / totalCount
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        TopCountriesList.ItemsSource = topCountries;

        TopCountriesList.ItemsSource = topCountries;
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
            ComputeDashboard();
    }

    private void FilterRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || FilterBlockedRadio == null || FilterAllowedRadio == null) return;

        if (FilterBlockedRadio.IsChecked == true)
            _verdictFilter = StatVerdictFilter.BlockedOnly;
        else if (FilterAllowedRadio.IsChecked == true)
            _verdictFilter = StatVerdictFilter.AllowedOnly;
        else
            _verdictFilter = StatVerdictFilter.All;

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
            ConnectionLogReader.Reset();
            LoadAndComputeStats();
        }
    }
}

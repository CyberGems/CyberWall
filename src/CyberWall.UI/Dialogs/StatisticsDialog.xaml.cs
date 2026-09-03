using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Services;
using WpfClipboard = System.Windows.Clipboard;

namespace CyberWall.UI.Dialogs;

public enum StatPeriod
{
    Today,
    Last24Hours,
    Last7Days,
    Last30Days,
    AllTime
}

public enum StatVerdictFilter
{
    All,
    BlockedOnly,
    AllowedOnly
}

public class StatItem
{
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool HasCountry { get; set; }
    public string CountryLabel { get; set; } = "";
    public int Count { get; set; }
    public int BlockedCount { get; set; }
    public int AllowedCount { get; set; }
    public double Percentage { get; set; }
    public string PercentageFormatted => $"{Percentage:F1}%";
    public string CountFormatted => Count.ToString("N0", CultureInfo.CurrentCulture);
    public string TooltipText { get; set; } = "";
}

internal class ParsedLogEvent
{
    public DateTime Timestamp { get; set; }
    public Verdict Verdict { get; set; }
    public Direction Direction { get; set; }
    public string AppPath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RemoteEndpoint { get; set; } = "";
    public string RemoteAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string ProcessId { get; set; } = "";
    public GeoResult Geo { get; set; } = GeoResult.Unknown;
}

public partial class StatisticsDialog : Window
{
    private readonly List<ParsedLogEvent> _allEvents = new();
    private bool _isInitializing = true;
    private StatVerdictFilter _verdictFilter = StatVerdictFilter.All;
    private readonly DispatcherTimer _autoRefreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private long _lastKnownLogLength = -1;

    public StatisticsDialog()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        CyberWallWindowChrome.Apply(this, 12);

        GeoCountry.Updated += OnGeoUpdated;
        Closed += (_, _) =>
        {
            _autoRefreshTimer.Stop();
            GeoCountry.Updated -= OnGeoUpdated;
        };
        GeoCountry.Warm();

        KeyDown += StatisticsDialog_KeyDown;

        PopulatePeriods();
        RefreshLanguage();
        _isInitializing = false;

        LoadAndComputeStats();

        _autoRefreshTimer.Tick += (_, _) => AutoRefreshTick();
        _autoRefreshTimer.Start();
    }

    private void AutoRefreshTick()
    {
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

    private void StatisticsDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F5)
        {
            LoadAndComputeStats();
            e.Handled = true;
        }
    }

    private void PopulatePeriods()
    {
        PeriodCombo.Items.Clear();
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriodToday"), Tag = StatPeriod.Today });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod24h"), Tag = StatPeriod.Last24Hours });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod7d"), Tag = StatPeriod.Last7Days });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriod30d"), Tag = StatPeriod.Last30Days });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Strings.T("StatsPeriodAll"), Tag = StatPeriod.AllTime });

        // Default to Last 24 Hours
        PeriodCombo.SelectedIndex = 1;
    }

    public void RefreshLanguage()
    {
        Title = Strings.T("StatsTitle");
        TitleLbl.Text = Strings.T("StatsHeaderTitle");
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
        TopPortsTitle.Text = Strings.T("StatsTopPorts");

        EmptyMsgTitle.Text = Strings.T("StatsNoData");
        EmptyMsgDesc.Text = Strings.T("StatsNoData");

        ViewLogBtnText.Text = Strings.T("StatsViewLog");
        CopySummaryBtnText.Text = Strings.T("StatsCopySummary");
        ClearStatsBtnText.Text = Strings.T("StatsResetBtn");
        ClearStatsBtn.ToolTip = Strings.T("StatsResetTitle");
        CloseBtn.Content = Strings.T("Close");

        UpdateSessionVolume();
    }

    private static string SafeGetDisplayName(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "System";
        try
        {
            var fn = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(fn) ? path : fn;
        }
        catch
        {
            return path;
        }
    }

    private void LoadAndComputeStats(bool silent = false)
    {
        var path = BlockedLog.LogPath;
        var newEvents = new List<ParsedLogEvent>(4000);

        if (File.Exists(path))
        {
            try
            {
                _lastKnownLogLength = new FileInfo(path).Length;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                while (sr.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 5)
                    {
                        var tsStr = parts[0];
                        if (!DateTime.TryParseExact(tsStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts))
                        {
                            if (!DateTime.TryParse(tsStr, CultureInfo.CurrentCulture, DateTimeStyles.None, out ts))
                                ts = DateTime.Now;
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

                        newEvents.Add(new ParsedLogEvent
                        {
                            Timestamp = ts,
                            Verdict = verd,
                            Direction = dir,
                            AppPath = app,
                            DisplayName = SafeGetDisplayName(app),
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

        _allEvents.Clear();
        _allEvents.AddRange(newEvents);

        ComputeDashboard();
    }

    private void ComputeDashboard()
    {
        UpdateSessionVolume();

        var period = StatPeriod.Last24Hours;
        if (PeriodCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is StatPeriod p)
            period = p;

        var cutoff = period switch
        {
            StatPeriod.Today => DateTime.Today,
            StatPeriod.Last24Hours => DateTime.Now.AddHours(-24),
            StatPeriod.Last7Days => DateTime.Now.AddDays(-7),
            StatPeriod.Last30Days => DateTime.Now.AddDays(-30),
            _ => DateTime.MinValue
        };

        var events = _allEvents.Where(e => e.Timestamp >= cutoff).ToList();
        int total = events.Count;

        // KPI Counts across entire period
        int blocked = events.Count(e => e.Verdict == Verdict.Block);
        int allowed = total - blocked;
        double blockedPct = total > 0 ? (blocked * 100.0 / total) : 0;
        double allowedPct = total > 0 ? (allowed * 100.0 / total) : 0;

        CountBadge.Text = _verdictFilter switch
        {
            StatVerdictFilter.BlockedOnly => $"{blocked:N0} " + Strings.T("StatsBlocked").ToLower(),
            StatVerdictFilter.AllowedOnly => $"{allowed:N0} " + Strings.T("StatsAllowed").ToLower(),
            _ => Strings.T("StatsEventsCount", total.ToString("N0", CultureInfo.CurrentCulture))
        };

        if (total == 0)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            EmptyStateView.Visibility = Visibility.Visible;
            TotalEventsVal.Text = "0";
            BlockedEventsVal.Text = "0";
            BlockedPctVal.Text = " (0%)";
            AllowedEventsVal.Text = "0";
            AllowedPctVal.Text = " (0%)";
            ScopeVal.Text = Strings.T("StatsCountriesCount", 0);
            CardScopeDesc.Text = Strings.T("StatsScopeDetail", 0, 0);
            return;
        }

        DashboardView.Visibility = Visibility.Visible;
        EmptyStateView.Visibility = Visibility.Collapsed;

        // 1. KPI Cards
        int uniqueApps = events.Select(e => e.AppPath).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var uniqueCountries = events.Where(e => e.Geo.HasCountry).Select(e => e.Geo.Iso2).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        TotalEventsVal.Text = total.ToString("N0", CultureInfo.CurrentCulture);
        BlockedEventsVal.Text = blocked.ToString("N0", CultureInfo.CurrentCulture);
        BlockedPctVal.Text = $" ({blockedPct:F1}%)";

        AllowedEventsVal.Text = allowed.ToString("N0", CultureInfo.CurrentCulture);
        AllowedPctVal.Text = $" ({allowedPct:F1}%)";

        int uniqueIps = events.Select(e => e.RemoteAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int localCount = events.Count(e => e.Geo.Kind == GeoKind.Local);

        ScopeVal.Text = Strings.T("StatsCountriesCount", uniqueCountries);
        CardScopeDesc.Text = Strings.T("StatsScopeDetail", uniqueApps, uniqueIps);
        CardScopeBorder.ToolTip = $"{uniqueCountries} {Strings.T("StatsCountriesCount", uniqueCountries)}\n{uniqueApps} {Strings.T("StatsTopApps")}\n{uniqueIps} {Strings.T("StatsUniqueIpsWithLocal", uniqueIps, localCount)}";

        // Filter events for breakdown views based on selected verdict filter
        var breakdownEvents = _verdictFilter switch
        {
            StatVerdictFilter.BlockedOnly => events.Where(e => e.Verdict == Verdict.Block).ToList(),
            StatVerdictFilter.AllowedOnly => events.Where(e => e.Verdict == Verdict.Allow).ToList(),
            _ => events
        };

        int breakdownTotal = Math.Max(1, breakdownEvents.Count);

        // 2. Top 5 Applications
        var topApps = breakdownEvents
            .GroupBy(e => e.AppPath, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var appTotal = g.Count();
                var appBlocked = g.Count(x => x.Verdict == Verdict.Block);
                var appAllowed = appTotal - appBlocked;
                var dispName = SafeGetDisplayName(g.Key);
                var pct = (appTotal * 100.0) / breakdownTotal;

                var tooltip = $"{dispName}\n{Strings.T("StatsTotalEvents")}: {appTotal:N0} ({pct:F1}%)\n{Strings.T("StatsBlocked")}: {appBlocked:N0}\n{Strings.T("StatsAllowed")}: {appAllowed:N0}\n{g.Key}";

                return new StatItem
                {
                    Name = dispName,
                    IconPath = g.Key,
                    Count = appTotal,
                    BlockedCount = appBlocked,
                    AllowedCount = appAllowed,
                    Percentage = pct,
                    TooltipText = tooltip
                };
            })
            .OrderByDescending(i => i.Count)
            .Take(5)
            .ToList();

        TopAppsList.ItemsSource = topApps;

        // 3. Traffic Direction (Outbound vs Inbound)
        int outbound = breakdownEvents.Count(e => e.Direction == Direction.Outbound);
        int inbound = breakdownEvents.Count - outbound;
        double outPct = breakdownTotal > 0 ? (outbound * 100.0 / breakdownTotal) : 0;
        double inPct = breakdownTotal > 0 ? (inbound * 100.0 / breakdownTotal) : 0;

        OutboundVal.Text = $"{outbound:N0} ({outPct:F1}%)";
        InboundVal.Text = $"{inbound:N0} ({inPct:F1}%)";

        double starOut = Math.Max(outbound, 0.1);
        double starIn = Math.Max(inbound, 0.1);
        OutboundBarCol.Width = new GridLength(starOut, GridUnitType.Star);
        InboundBarCol.Width = new GridLength(starIn, GridUnitType.Star);

        // 4. Top 5 Destination Countries
        var countryGroups = breakdownEvents
            .Where(e => e.Geo.Kind == GeoKind.Country && e.Geo.HasCountry)
            .GroupBy(e => e.Geo.Iso2 ?? "")
            .Select(g =>
            {
                var first = g.First();
                return new StatItem
                {
                    CountryCode = first.Geo.Iso2 ?? "",
                    HasCountry = true,
                    CountryLabel = CountryDisplay.Label(first.Geo),
                    Count = g.Count(),
                    Percentage = (g.Count() * 100.0) / breakdownTotal
                };
            })
            .OrderByDescending(i => i.Count)
            .Take(5)
            .ToList();

        TopCountriesList.ItemsSource = countryGroups;

        // 5. Top 5 Ports & Protocols
        var portGroups = breakdownEvents
            .Where(e => e.RemotePort > 0)
            .GroupBy(e => e.RemotePort)
            .Select(g =>
            {
                var port = g.Key;
                var service = NetworkEndpoint.ServiceLabel("TCP", port);
                return new StatItem
                {
                    Name = $"{port}",
                    Subtitle = service,
                    Count = g.Count(),
                    Percentage = (g.Count() * 100.0) / breakdownTotal
                };
            })
            .OrderByDescending(i => i.Count)
            .Take(5)
            .ToList();

        TopPortsList.ItemsSource = portGroups;
    }

    private void OnGeoUpdated()
    {
        Dispatcher.BeginInvoke(ComputeDashboard);
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        ComputeDashboard();
    }

    private void FilterRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
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
        var confirmed = ConfirmDialog.Show(
            this,
            Strings.T("StatsResetTitle"),
            Strings.T("StatsResetConfirm"),
            Strings.T("StatsResetBtn"),
            Strings.T("Cancel"),
            ConfirmIconType.Trash);

        if (confirmed)
        {
            try
            {
                BlockedLog.Clear();
                _allEvents.Clear();
                LoadAndComputeStats();
            }
            catch { }
        }
    }

    private void ViewLog_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new LogViewerDialog { Owner = this.Owner ?? this };
        dlg.ShowDialog();
        LoadAndComputeStats();
    }

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var periodText = (PeriodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            var filterText = _verdictFilter switch
            {
                StatVerdictFilter.BlockedOnly => Strings.T("StatsFilterBlocked"),
                StatVerdictFilter.AllowedOnly => Strings.T("StatsFilterAllowed"),
                _ => Strings.T("StatsFilterAll")
            };

            var sb = new StringBuilder();
            sb.AppendLine($"=== {Strings.T("StatsSummaryHeader")} ===");
            sb.AppendLine($"{Strings.T("StatsPeriodLabel")} {periodText} | Filtro: {filterText}");
            sb.AppendLine($"{Strings.T("DateTime")}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"{Strings.T("StatsTotalEvents")}: {TotalEventsVal.Text}");
            sb.AppendLine($"{Strings.T("StatsBlocked")}: {BlockedEventsVal.Text}{BlockedPctVal.Text}");
            sb.AppendLine($"{Strings.T("StatsAllowed")}: {AllowedEventsVal.Text}{AllowedPctVal.Text}");
            sb.AppendLine($"{Strings.T("StatsDataVolume")}: {DataVolumeVal.Text} | {DataVolumeDesc.Text}");
            sb.AppendLine($"{Strings.T("StatsGlobalScope")}: {ScopeVal.Text} ({CardScopeDesc.Text})");
            sb.AppendLine($"{Strings.T("StatsTrafficDirection")}: Outbound {OutboundVal.Text} | Inbound {InboundVal.Text}");
            sb.AppendLine("--------------------------------------------------");

            if (TopAppsList.ItemsSource is IEnumerable<StatItem> apps && apps.Any())
            {
                sb.AppendLine($"[{Strings.T("StatsTopApps")}]");
                foreach (var a in apps)
                    sb.AppendLine($" - {a.Name}: {a.CountFormatted} ({a.PercentageFormatted}) [B: {a.BlockedCount} | A: {a.AllowedCount}]");
                sb.AppendLine();
            }

            if (TopCountriesList.ItemsSource is IEnumerable<StatItem> countries && countries.Any())
            {
                sb.AppendLine($"[{Strings.T("StatsTopCountries")}]");
                foreach (var c in countries)
                    sb.AppendLine($" - {c.CountryLabel}: {c.CountFormatted} ({c.PercentageFormatted})");
                sb.AppendLine();
            }

            if (TopPortsList.ItemsSource is IEnumerable<StatItem> ports && ports.Any())
            {
                sb.AppendLine($"[{Strings.T("StatsTopPorts")}]");
                foreach (var p in ports)
                    sb.AppendLine($" - Port {p.Name} ({p.Subtitle}): {p.CountFormatted} ({p.PercentageFormatted})");
                sb.AppendLine();
            }

            sb.AppendLine("==================================================");

            WpfClipboard.SetText(sb.ToString());
            ConfirmDialog.Show(this, "CyberWall", Strings.T("StatsSummaryCopied"), Strings.T("Ok"), null, ConfirmIconType.Check);
        }
        catch { }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

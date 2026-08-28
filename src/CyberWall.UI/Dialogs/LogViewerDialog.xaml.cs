using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Services;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;

namespace CyberWall.UI.Dialogs;

public class LogItem
{
    public string Timestamp { get; set; } = "";
    public Verdict Verdict { get; set; }
    public Direction Direction { get; set; }
    public string AppPath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RemoteEndpoint { get; set; } = "";
    public string ProcessId { get; set; } = "";
    public string RawLine { get; set; } = "";
    public GeoResult Geo { get; set; } = GeoResult.Unknown;
    public bool HasCountry => Geo.HasCountry;
    public string CountryCode => Geo.HasCountry ? Geo.Iso2 ?? "" : "";
    public string CountryLabel => CountryDisplay.Label(Geo);
}

public partial class LogViewerDialog : Window
{
    private List<LogItem> _allItems = new();

    public LogViewerDialog()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        RefreshLanguage();
        GeoCountry.Updated += OnGeoUpdated;
        Closed += (_, _) => GeoCountry.Updated -= OnGeoUpdated;
        GeoCountry.Warm();
        LoadLogs();
    }

    private void RefreshLanguage()
    {
        TitleLbl.Text = Strings.T("LogViewerTitle");
        SearchPlaceholder.Text = Strings.T("SearchLog");
        RefreshBtnText.Text = Strings.T("Refresh");
        CopyBtnText.Text = Strings.T("CopyAll");
        ClearBtnText.Text = Strings.T("ClearLog");
        OpenFileBtnText.Text = Strings.T("OpenFile");
        CloseBtn.Content = Strings.T("Close");
        EmptyMsg.Text = Strings.T("NoLogs");
        PathInfoText.Text = BlockedLog.LogPath;
        ColTime.Header = Strings.T("DateTime");
        ColAction.Header = Strings.T("Action");
        ColDir.Header = Strings.T("Direction");
        ColProg.Header = Strings.T("Program");
        ColCountry.Header = Strings.T("Country");
        ColDest.Header = Strings.T("Destination");
    }

    private void LoadLogs()
    {
        _allItems.Clear();
        var path = BlockedLog.LogPath;
        if (File.Exists(path))
        {
            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 5)
                    {
                        var ts = parts[0];
                        var verd = parts[1].Equals("Block", StringComparison.OrdinalIgnoreCase) ? Verdict.Block : Verdict.Allow;
                        var dir = parts[2].Equals("Inbound", StringComparison.OrdinalIgnoreCase) ? Direction.Inbound : Direction.Outbound;
                        var app = parts[3];
                        var ep = parts[4];
                        var pid = parts.Length > 5 ? parts[5] : "";
                        var geo = GeoCountry.Lookup(NetworkEndpoint.ExtractAddress(ep));

                        _allItems.Add(new LogItem
                        {
                            Timestamp = ts,
                            Verdict = verd,
                            Direction = dir,
                            AppPath = app,
                            DisplayName = Path.GetFileName(app),
                            RemoteEndpoint = ep,
                            ProcessId = pid,
                            RawLine = line,
                            Geo = geo
                        });
                    }
                }
            }
            catch { }
        }

        _allItems.Reverse(); // Most recent entries first
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _allItems
            : _allItems.Where(i =>
                i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.AppPath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.RemoteEndpoint.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.ProcessId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.CountryLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Timestamp.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Verdict.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        LogGrid.ItemsSource = null;
        LogGrid.ItemsSource = filtered;
        LogGrid.Visibility = filtered.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CountBadge.Text = Strings.T("EntriesCount", filtered.Count);
        EmptyMsg.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnGeoUpdated() => Dispatcher.BeginInvoke(LoadLogs);

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadLogs();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = string.Join(Environment.NewLine, _allItems.Select(i => i.RawLine));
            if (!string.IsNullOrEmpty(text))
            {
                WpfClipboard.SetText(text);
                WpfMessageBox.Show(Strings.T("CopiedToClipboard"), "CyberWall", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch { }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var res = WpfMessageBox.Show(Strings.T("ClearLogConfirm"), "CyberWall", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                BlockedLog.Clear();
                _allItems.Clear();
                ApplyFilter();
            }
            catch { }
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = BlockedLog.LogPath;
            if (!File.Exists(p))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, string.Empty);
            }
            Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
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

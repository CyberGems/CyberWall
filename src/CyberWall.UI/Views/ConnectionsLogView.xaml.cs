using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Services;
using WpfClipboard = System.Windows.Clipboard;
using UserControl = System.Windows.Controls.UserControl;

namespace CyberWall.UI.Views;

public partial class ConnectionsLogView : UserControl
{
    private List<LogItem> _allItems = new();
    private bool _isActive;

    public ConnectionsLogView()
    {
        InitializeComponent();
        RefreshLanguage();
        GeoCountry.Updated += OnGeoUpdated;
    }

    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        LoadLogs();
    }

    public void Deactivate()
    {
        _isActive = false;
    }

    private void OnGeoUpdated()
    {
        if (!_isActive) return;
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var item in _allItems)
            {
                if (!item.HasCountry)
                {
                    item.Geo = GeoCountry.Lookup(NetworkEndpoint.ExtractAddress(item.RemoteEndpoint));
                }
            }
            ApplyFilter();
        });
    }

    public void RefreshLanguage()
    {
        SearchPlaceholder.Text = Strings.T("SearchLog");
        RefreshBtnText.Text = Strings.T("Refresh");
        CopyBtnText.Text = Strings.T("CopyAll");
        ClearBtnText.Text = Strings.T("ClearLog");
        OpenFileBtnText.Text = Strings.T("OpenFile");
        EmptyMsg.Text = Strings.T("NoLogs");
        PathInfoText.Text = BlockedLog.LogPath;
        ColTime.Header = Strings.T("DateTime");
        ColAction.Header = Strings.T("Action");
        ColDir.Header = Strings.T("Direction");
        ColProg.Header = Strings.T("Program");
        ColCountry.Header = Strings.T("Country");
        ColDest.Header = Strings.T("Destination");

        FilterAllRadio.Content = Strings.T("All");
        FilterBlockedRadio.Content = Strings.T("Block");
        FilterAllowedRadio.Content = Strings.T("Allow");
    }

    public void LoadLogs()
    {
        _allItems.Clear();
        var path = BlockedLog.LogPath;
        if (File.Exists(path))
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, System.Text.Encoding.UTF8);
                while (sr.ReadLine() is { } line)
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

        _allItems.Reverse(); // Most recent first
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox?.Text?.Trim() ?? "";
        bool filterBlockedOnly = FilterBlockedRadio.IsChecked == true;
        bool filterAllowedOnly = FilterAllowedRadio.IsChecked == true;

        IEnumerable<LogItem> q = _allItems;

        if (filterBlockedOnly)
            q = q.Where(x => x.Verdict == Verdict.Block);
        else if (filterAllowedOnly)
            q = q.Where(x => x.Verdict == Verdict.Allow);

        if (!string.IsNullOrEmpty(filter))
        {
            q = q.Where(x =>
                x.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.AppPath.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.RemoteEndpoint.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.ProcessId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.CountryLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.CountryCode.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var list = q.ToList();
        LogGrid.ItemsSource = list;
        CountBadge.Text = Strings.T("EntriesCount", list.Count);
        EmptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadLogs();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (LogGrid.ItemsSource is not List<LogItem> items || items.Count == 0) return;
        var text = string.Join(Environment.NewLine, items.Select(x => x.RawLine));
        try
        {
            WpfClipboard.SetText(text);
        }
        catch { }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        var res = ConfirmDialog.Show(
            parentWindow,
            Strings.T("ClearLog"),
            Strings.T("ClearLogConfirm"),
            Strings.T("ClearLog"),
            Strings.T("Cancel"));

        if (res)
        {
            BlockedLog.Clear();
            LoadLogs();
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var path = BlockedLog.LogPath;
        if (File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void ContextSearch_Click(object sender, RoutedEventArgs e)
    {
        if (LogGrid.SelectedItem is LogItem item && !string.IsNullOrWhiteSpace(item.DisplayName))
        {
            try
            {
                var query = Uri.EscapeDataString($"{item.DisplayName} process windows");
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void ContextCopyDest_Click(object sender, RoutedEventArgs e)
    {
        if (LogGrid.SelectedItem is LogItem item && !string.IsNullOrWhiteSpace(item.RemoteEndpoint))
        {
            try
            {
                WpfClipboard.SetText(item.RemoteEndpoint);
            }
            catch { }
        }
    }

    private void ContextFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LogGrid.SelectedItem is LogItem item && !string.IsNullOrWhiteSpace(item.AppPath) && File.Exists(item.AppPath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{item.AppPath}\"");
            }
            catch { }
        }
    }
}

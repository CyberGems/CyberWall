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
    private IReadOnlyList<ConnectionLogEntry> _entries = Array.Empty<ConnectionLogEntry>();
    private bool _isInitializing = true;
    private bool _isActive;

    public ConnectionsLogView()
    {
        _isInitializing = true;
        InitializeComponent();
        _isInitializing = false;
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
            ConnectionLogGeo.Resolve(_entries, force: true);
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
        _entries = ConnectionLogReader.Refresh();
        ConnectionLogGeo.Resolve(_entries);

        foreach (var entry in _entries)
        {
            _allItems.Add(new LogItem
            {
                Timestamp = entry.TimestampText,
                Verdict = entry.Verdict,
                Direction = entry.Direction,
                AppPath = entry.AppPath,
                DisplayName = entry.DisplayName,
                RemoteEndpoint = entry.RemoteEndpoint,
                ProcessId = entry.ProcessId,
                RawLine = entry.RawLine,
                Geo = entry.Geo
            });
        }

        _allItems.Reverse(); // Most recent first
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_isInitializing || FilterBlockedRadio == null || FilterAllowedRadio == null || LogGrid == null || CountBadge == null || EmptyState == null)
            return;

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
        if (_isInitializing) return;
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
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
            ConnectionLogReader.Reset();
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

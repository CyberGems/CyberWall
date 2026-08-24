using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Popup;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public partial class MainWindow : Window
{
    private readonly FirewallService _svc = new();
    private List<AppRule> _all = new();
    private bool _loading;
    private readonly HashSet<string> _pendingPopups = new();
    private TrayService? _tray;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        _loading = true;
        Strings.Current = App.Settings.Language;
        ThemeManager.Apply(App.Settings.Theme);
        _svc.OnAskConnection += OnAskConnection;
        if (App.Settings.FirewallEnabled)
            _svc.Enable((FirewallMode)App.Settings.FirewallMode);
        RefreshRules();
        RefreshLanguage();
        UpdateStatus();
        var isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        if (!isAdmin) StatusText.Text += "  ⚠️ " + (Strings.Current == Lang.Es ? "Ejecuta como Admin para filtrado real" : "Run as Admin for kernel filtering");
        _tray = new TrayService(this);
        Closing += (_, _) => { App.Settings.FirewallEnabled = _svc.IsMasterOn; App.Settings.FirewallMode = (int)_svc.Mode; App.Settings.Save(); _svc.Dispose(); };
        Closed += (_, _) => _tray?.Dispose();
        _loading = false;
    }

    public void RefreshLanguage()
    {
        TitleText.Text = "CyberWall";
        SettingsBtn.Content = "⚙ " + Strings.T("Settings");
        ModeLbl.Text = Strings.T("Mode");
        SearchPlaceholder.Text = Strings.T("SearchPlaceholder");
        HdrProg.Text = Strings.T("Program") + (_sortBy == "DisplayName" ? (_sortAsc ? " ▾" : " ▴") : "");
        HdrPath.Text = Strings.T("Path") + (_sortBy == "AppPath" ? (_sortAsc ? " ▾" : " ▴") : "");
        HdrVerd.Text = Strings.T("Verdict");
        HdrDir.Text = Strings.T("Direction");
        AllowedExpander.Header = $"{Strings.T("Allowed")} ({_all.Count(r => r.Verdict == Verdict.Allow)})";
        BlockedExpander.Header = $"{Strings.T("Blocked")} ({_all.Count(r => r.Verdict == Verdict.Block)})";
        var m = _svc.Mode == FirewallMode.BlockAll ? 1 : 0;
        _loading = true;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeAsk") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeBlockAll") });
        ModeBox.SelectedIndex = m;
        _loading = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnAskConnection(ConnectionEvent ev)
    {
        Dispatcher.Invoke(() =>
        {
            var key = AppRule.Normalize(ev.AppPath);
            if (!_pendingPopups.Add(key)) return;
            var p = new ConnectionPopup(ev);
            p.ClosedWithVerdict += popup =>
            {
                _pendingPopups.Remove(key);
                _svc.SetVerdict(popup.Event.AppPath, popup.ResultVerdict, popup.Remember, popup.Event);
                if (popup.Remember) RefreshRules(SearchBox.Text);
            };
            p.Closed += (_, _) => _pendingPopups.Remove(key);
            p.Show();
        });
    }

    private string _sortBy = "DisplayName";
    private bool _sortAsc = true;

    private void RefreshRules(string? filter = null)
    {
        _all = _svc.Store.All.ToList();
        var q = string.IsNullOrWhiteSpace(filter) ? _all : _all.Where(r => r.DisplayName.Contains(filter!, StringComparison.OrdinalIgnoreCase) || r.AppPath.Contains(filter!, StringComparison.OrdinalIgnoreCase)).ToList();
        Func<AppRule, object> key = _sortBy == "AppPath" ? r => r.AppPath : r => r.DisplayName;
        var blocked = q.Where(r => r.Verdict == Verdict.Block);
        var allowed = q.Where(r => r.Verdict == Verdict.Allow);
        blocked = _sortAsc ? blocked.OrderBy(key) : blocked.OrderByDescending(key);
        allowed = _sortAsc ? allowed.OrderBy(key) : allowed.OrderByDescending(key);
        BlockedGrid.ItemsSource = blocked.ToList();
        AllowedGrid.ItemsSource = allowed.ToList();
        RefreshLanguage();
    }

    private void SortProg_Click(object sender, MouseButtonEventArgs e)
    {
        if (_sortBy == "DisplayName") _sortAsc = !_sortAsc; else { _sortBy = "DisplayName"; _sortAsc = true; }
        RefreshRules(SearchBox.Text);
    }

    private void SortPath_Click(object sender, MouseButtonEventArgs e)
    {
        if (_sortBy == "AppPath") _sortAsc = !_sortAsc; else { _sortBy = "AppPath"; _sortAsc = true; }
        RefreshRules(SearchBox.Text);
    }

    private void UpdateStatus()
    {
        var on = _svc.IsMasterOn;
        MasterToggle.IsChecked = on;
        MasterLabel.Text = on ? Strings.T("ProtectionActive") : Strings.T("ProtectionDisabled");
        StatusDot.Fill = on ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
        ModeBox.IsEnabled = on;
        var real = _svc.Wfp.IsRealBlock ? " • WFP Real" : " • Simulado";
        if (!on) StatusText.Text = Strings.T("StatusDisabled") + real;
        else StatusText.Text = (_svc.Mode == FirewallMode.Ask ? Strings.T("StatusEnabledAsk") : Strings.T("StatusEnabledBlock")) + real;
    }

    private void MasterToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (MasterToggle.IsChecked == true) { var m = ModeBox.SelectedIndex == 1 ? FirewallMode.BlockAll : FirewallMode.Ask; _svc.Enable(m); }
        else _svc.Disable();
        App.Settings.FirewallEnabled = _svc.IsMasterOn;
        App.Settings.FirewallMode = (int)_svc.Mode;
        App.Settings.Save();
        UpdateStatus();
    }

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_svc.IsMasterOn) return;
        var m = ModeBox.SelectedIndex == 1 ? FirewallMode.BlockAll : FirewallMode.Ask;
        _svc.SetMode(m);
        App.Settings.FirewallMode = (int)m;
        App.Settings.Save();
        UpdateStatus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RefreshRules(SearchBox.Text);
    }

    private void QuickRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn || btn.Tag is not AppRule r) return;
        var dlg = new ConfirmDialog(r.DisplayName, r.AppPath) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _svc.RemoveRule(r.AppPath);
            RefreshRules(SearchBox.Text);
        }
    }

    private void RuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is AppRule r)
        {
            var allow = cb.IsChecked == true;
            _svc.SetVerdict(r.AppPath, allow ? Verdict.Allow : Verdict.Block, true, null);
            RefreshRules(SearchBox.Text);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) { var w = new SettingsWindow(App.Settings) { Owner = this }; w.ShowDialog(); RefreshLanguage(); UpdateStatus(); }
    private void OpenLog_Click(object sender, MouseButtonEventArgs e) { try { var p = BlockedLog.LogPath; if (System.IO.File.Exists(p)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = true }); else System.Windows.MessageBox.Show((Strings.Current == Lang.Es ? $"Aún sin bloqueos.\n{p}" : $"No blocked connections yet.\n{p}")); LogPathText.Text = p; } catch { } }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        RulesScrollViewer.ScrollToVerticalOffset(RulesScrollViewer.VerticalOffset - (e.Delta / 2.0));
        e.Handled = true;
    }
}

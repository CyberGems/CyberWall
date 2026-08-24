using System.Windows;
using System.Windows.Controls;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;
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
        _loading = true;
        Strings.Current = App.Settings.Language;
        _svc.OnAskConnection += OnAskConnection;
        if (App.Settings.FirewallEnabled)
            _svc.Enable((FirewallMode)App.Settings.FirewallMode);
        RefreshRules();
        RefreshLanguage();
        UpdateStatus();
        var isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        if (!isAdmin) StatusText.Text += "  \u26a0 Ejecuta como Admin para bloqueo real (ahora simulado)";
        _tray = new TrayService(this);
        Closing += (_, _) => { App.Settings.FirewallEnabled = _svc.IsMasterOn; App.Settings.FirewallMode = (int)_svc.Mode; App.Settings.Save(); _svc.Dispose(); };
        Closed += (_, _) => _tray?.Dispose();
        _loading = false;
    }

    public void RefreshLanguage()
    {
        var es = Strings.Current == Lang.Es;
        TitleText.Text = Strings.T("AppTitle");
        SettingsBtn.Content = es ? "\u2699 Configuraci\u00f3n" : "\u2699 Settings";
        ModeLbl.Text = es ? "Modo:" : "Mode:";
        HintText.Text = es ? "  Reglas por programa \u2014 cada .exe nuevo dispara popup (no por IP)" : "  Per-program rules \u2014 each new .exe triggers popup (not per IP)";
        RemoveBtn.Content = es ? "Quitar regla" : "Remove rule";
        HdrProg.Text = (es ? "Programa" : "Program") + (_sortBy == "DisplayName" ? (_sortAsc ? " \u25BE" : " \u25B4") : "");
        HdrPath.Text = es ? "Ruta" : "Path";
        HdrVerd.Text = es ? "Veredicto" : "Verdict";
        HdrDir.Text = es ? "Direcci\u00f3n" : "Direction";
        AllowedExpander.Header = $"{(es ? "Permitidas" : "Allowed")} ({_all.Count(r => r.Verdict == Verdict.Allow)})";
        BlockedExpander.Header = $"{(es ? "Bloqueadas" : "Blocked")} ({_all.Count(r => r.Verdict == Verdict.Block)})";
        var m = _svc.Mode == FirewallMode.BlockAll ? 1 : 0;
        _loading = true;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeAsk") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeBlockAll") });
        ModeBox.SelectedIndex = m;
        _loading = false;
    }

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

    private void SortProg_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_sortBy == "DisplayName") _sortAsc = !_sortAsc; else { _sortBy = "DisplayName"; _sortAsc = true; }
        RefreshRules(SearchBox.Text);
    }

    private void SortPath_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_sortBy == "AppPath") _sortAsc = !_sortAsc; else { _sortBy = "AppPath"; _sortAsc = true; }
        RefreshRules(SearchBox.Text);
    }

    private void UpdateStatus()
    {
        var on = _svc.IsMasterOn;
        MasterToggle.IsChecked = on;
        MasterLabel.Text = on ? Strings.T("MasterOn") : Strings.T("MasterOff");
        MasterLabel.Foreground = on ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.IndianRed;
        ModeBox.IsEnabled = on;
        var real = _svc.Wfp.IsRealBlock ? " \u2022 WFP real" : " \u2022 simulado";
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

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshRules(SearchBox.Text);
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var r = (BlockedGrid.SelectedItem as AppRule) ?? (AllowedGrid.SelectedItem as AppRule);
        if (r != null) { _svc.RemoveRule(r.AppPath); RefreshRules(SearchBox.Text); }
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
    private void TestPopup_Click(object sender, RoutedEventArgs e) { var ev = new ConnectionEvent { AppPath = $@"C:\Program Files\Demo\demo{Random.Shared.Next(1000)}.exe", RemoteAddress = "142.250.0.1", RemotePort = 443, Direction = Direction.Outbound, ProcessId = Random.Shared.Next(1000, 9999) }; OnAskConnection(ev); }
    private void Settings_Click(object sender, RoutedEventArgs e) { var w = new SettingsWindow(App.Settings) { Owner = this }; w.ShowDialog(); RefreshLanguage(); UpdateStatus(); }
    private void OpenLog_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) { try { var p = BlockedLog.LogPath; if (System.IO.File.Exists(p)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = true }); else System.Windows.MessageBox.Show($"A\u00fan sin bloqueos.\n{p}"); LogPathText.Text = p; } catch { } }
}

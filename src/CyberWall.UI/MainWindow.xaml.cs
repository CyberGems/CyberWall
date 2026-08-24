using System.Windows;
using System.Windows.Controls;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
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
        LangBox.SelectedIndex = Strings.Current == Lang.Es ? 0 : 1;
        ModeBox.SelectedIndex = 0;
        MasterToggle.IsChecked = true;
        var isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _svc.OnAskConnection += OnAskConnection;
        _svc.Enable(FirewallMode.Ask);
        RefreshRules();
        UpdateStatus();
        if (!isAdmin) StatusText.Text += "  \u26a0 Ejecuta como Admin para bloqueo real (ahora simulado)";
        _tray = new TrayService(this);
        Closing += (_, _) => _svc.Dispose();
        Closed += (_, _) => _tray?.Dispose();
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
                if (popup.ResultVerdict == Verdict.Allow || popup.ResultVerdict == Verdict.Block)
                {
                    _svc.SetVerdict(popup.Event.AppPath, popup.ResultVerdict, popup.Remember, popup.Event);
                    if (popup.Remember) RefreshRules(SearchBox.Text);
                }
            };
            p.Closed += (_, _) => _pendingPopups.Remove(key);
            p.Show();
            Activate();
        });
    }

    private void RefreshRules(string? filter = null)
    {
        _all = _svc.Store.All.ToList();
        var q = string.IsNullOrWhiteSpace(filter) ? _all : _all.Where(r => r.DisplayName.Contains(filter!, StringComparison.OrdinalIgnoreCase) || r.AppPath.Contains(filter!, StringComparison.OrdinalIgnoreCase)).ToList();
        RulesGrid.ItemsSource = q;
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
        TitleText.Text = Strings.T("AppTitle");
    }

    private void MasterToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (MasterToggle.IsChecked == true) { var m = ModeBox.SelectedIndex == 1 ? FirewallMode.BlockAll : FirewallMode.Ask; _svc.Enable(m); }
        else _svc.Disable();
        UpdateStatus();
    }

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !_svc.IsMasterOn) return;
        var m = ModeBox.SelectedIndex == 1 ? FirewallMode.BlockAll : FirewallMode.Ask;
        _svc.SetMode(m);
        UpdateStatus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshRules(SearchBox.Text);
    private void Remove_Click(object sender, RoutedEventArgs e) { if (RulesGrid.SelectedItem is AppRule r) { _svc.RemoveRule(r.AppPath); RefreshRules(SearchBox.Text); } }

    private void TestPopup_Click(object sender, RoutedEventArgs e)
    {
        var ev = new ConnectionEvent { AppPath = $@"C:\Program Files\Demo\demo{Random.Shared.Next(1000)}.exe", RemoteAddress = "142.250.0.1", RemotePort = 443, Direction = Direction.Outbound, ProcessId = Random.Shared.Next(1000, 9999) };
        OnAskConnection(ev);
    }

    private void LangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Strings.Current = LangBox.SelectedIndex == 0 ? Lang.Es : Lang.En;
        _loading = true;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeAsk") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeBlockAll") });
        ModeBox.SelectedIndex = _svc.Mode == FirewallMode.BlockAll ? 1 : 0;
        _loading = false;
        UpdateStatus();
    }

    private void OpenLog_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try { var p = BlockedLog.LogPath; if (System.IO.File.Exists(p)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = true }); else System.Windows.MessageBox.Show($"Aún sin bloqueos.\n{p}"); LogPathText.Text = p; } catch { }
    }
}

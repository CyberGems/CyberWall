using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private const int WM_NCHITTEST = 0x0084;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

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
        _tray = new TrayService(this, _svc);
        Closing += (_, _) => { App.Settings.FirewallEnabled = _svc.IsMasterOn; App.Settings.FirewallMode = (int)_svc.Mode; App.Settings.Save(); _svc.Dispose(); };
        Closed += (_, _) => _tray?.Dispose();
        StateChanged += (_, _) => UpdateMaximizeButtonIcon();
        UpdateMaximizeButtonIcon();
        _loading = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST && WindowState == WindowState.Normal)
        {
            int x = lParam.ToInt32() & 0xffff;
            int y = (lParam.ToInt32() >> 16) & 0xffff;
            if (x > 32767) x -= 65536;
            if (y > 32767) y -= 65536;

            var pt = PointFromScreen(new System.Windows.Point(x, y));
            const int b = 7;

            bool left = pt.X <= b;
            bool right = pt.X >= ActualWidth - b;
            bool top = pt.Y <= b;
            bool bottom = pt.Y >= ActualHeight - b;

            if (top && left) { handled = true; return (IntPtr)HTTOPLEFT; }
            if (top && right) { handled = true; return (IntPtr)HTTOPRIGHT; }
            if (bottom && left) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
            if (bottom && right) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (left) { handled = true; return (IntPtr)HTLEFT; }
            if (right) { handled = true; return (IntPtr)HTRIGHT; }
            if (top) { handled = true; return (IntPtr)HTTOP; }
            if (bottom) { handled = true; return (IntPtr)HTBOTTOM; }
        }
        return IntPtr.Zero;
    }

    public void RefreshLanguage()
    {
        TitleText.Text = "CyberWall";
        SettingsBtn.Content = "⚙ " + Strings.T("Settings");
        ModeLbl.Text = Strings.T("Mode");
        SearchPlaceholder.Text = Strings.T("SearchPlaceholder");
        ViewLogBtn.Content = "📋 " + Strings.T("ViewLog");

        var progHdr = Strings.T("Program") + (_sortBy == "DisplayName" ? (_sortAsc ? " ▾" : " ▴") : "");
        var pathHdr = Strings.T("Path") + (_sortBy == "AppPath" ? (_sortAsc ? " ▾" : " ▴") : "");
        var actHdr = Strings.T("Action");
        var dirHdr = Strings.T("Direction");
        var stateHdr = Strings.Current == Lang.Es ? "Estado" : "State";

        AllowColState.Header = stateHdr;
        AllowColProg.Header = progHdr;
        AllowColPath.Header = pathHdr;
        AllowColAction.Header = actHdr;
        AllowColDir.Header = dirHdr;

        BlockColState.Header = stateHdr;
        BlockColProg.Header = progHdr;
        BlockColPath.Header = pathHdr;
        BlockColAction.Header = actHdr;
        BlockColDir.Header = dirHdr;

        AllowedExpander.Header = $"{Strings.T("Allowed")} ({_all.Count(r => r.Verdict == Verdict.Allow)})";
        BlockedExpander.Header = $"{Strings.T("Blocked")} ({_all.Count(r => r.Verdict == Verdict.Block)})";
        var m = _svc.Mode == FirewallMode.BlockAll ? 1 : 0;
        _loading = true;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeAsk") });
        ModeBox.Items.Add(new ComboBoxItem { Content = Strings.T("ModeBlockAll") });
        ModeBox.SelectedIndex = m;
        _loading = false;
        UpdateMaximizeButtonIcon();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (MaximizeBtn == null || MaximizeIconPath == null) return;
        if (WindowState == WindowState.Maximized)
        {
            MaximizeBtn.ToolTip = Strings.T("Restore");
            MaximizeIconPath.Data = Geometry.Parse("M 6 2 L 6 6 L 2 6 M 6 6 L 2.5 2.5 M 8 12 L 8 8 L 12 8 M 8 8 L 11.5 11.5");
        }
        else
        {
            MaximizeBtn.ToolTip = Strings.T("Maximize");
            MaximizeIconPath.Data = Geometry.Parse("M 2 8 L 2 2 L 8 2 M 2 2 L 6 6 M 12 6 L 12 12 L 6 12 M 12 12 L 8 8");
        }
    }

    public void RefreshStatusFromExternal()
    {
        UpdateStatus();
    }

    public void ExitApplication()
    {
        _tray?.RequestExit();
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
    
    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new LogViewerDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        RulesScrollViewer.ScrollToVerticalOffset(RulesScrollViewer.VerticalOffset - (e.Delta / 2.0));
        e.Handled = true;
    }
}

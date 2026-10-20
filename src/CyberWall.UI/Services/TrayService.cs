using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Service.Engine;
using CyberWall.UI.Tray;
using WF = System.Windows.Forms;

namespace CyberWall.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly MainWindow _win;
    private readonly FirewallService _svc;
    private bool _exit;
    private bool _warnedAboutTray;

    public TrayService(MainWindow win, FirewallService svc)
    {
        _win = win;
        _svc = svc;
        _icon = new WF.NotifyIcon
        {
            Visible = true,
            Text = SafeTrayText(Strings.T("TrayTooltip")),
            Icon = AppIconHelper.CreateShieldIcon(32),
            ContextMenuStrip = null // Using custom premium WPF menu
        };

        _icon.MouseClick += (s, e) =>
        {
            if (e.Button == WF.MouseButtons.Left)
            {
                ToggleVisibility();
            }
            else if (e.Button == WF.MouseButtons.Right)
            {
                ShowContextMenu();
            }
        };

        _win.Closing += OnClosing;
        _icon.BalloonTipClicked += (_, _) =>
        {
            _win.ShowNotifications();
        };
    }

    /// <summary>
    /// Keeps the tray icon and its tooltip in sync with the protection state, so the
    /// user can tell at a glance that the firewall is still filtering traffic.
    /// </summary>
    public void UpdateState()
    {
        try
        {
            var active = _svc.OwnedByAnotherHost || _svc.IsMasterOn;
            _icon.Icon = AppIconHelper.CreateShieldIcon(32, muted: !active);

            var tooltip = _svc.OwnedByAnotherHost
                ? Strings.T("TrayTooltipService")
                : active
                    ? Strings.T("TrayTooltipActive")
                    : Strings.T("TrayTooltipOff");
            _icon.Text = SafeTrayText(tooltip);
        }
        catch { }
    }

    public void RefreshLanguage()
    {
        try
        {
            UpdateState();
        }
        catch { }
    }

    private static string SafeTrayText(string text) =>
        string.IsNullOrEmpty(text) ? "CyberWall" : (text.Length <= 63 ? text : text[..63]);

    public void NotifyAutoBlock(string appName)
    {
        try
        {
            _icon.BalloonTipTitle = Strings.T("AutoBlockedTitle");
            _icon.BalloonTipText = Strings.T("AutoBlockedDesc", appName);
            _icon.BalloonTipIcon = WF.ToolTipIcon.Warning;
            _icon.ShowBalloonTip(8000);
        }
        catch { }
    }

    public void ToggleVisibility()
    {
        if (_win.IsVisible && _win.WindowState != WindowState.Minimized)
        {
            _win.Hide();
        }
        else
        {
            _win.Show();
            _win.WindowState = WindowState.Normal;
            _win.Activate();
        }
    }

    private void ShowContextMenu()
    {
        var pt = WF.Cursor.Position;
        var menu = new TrayContextMenuWindow(_win, _svc, pt);
        menu.Show();
    }

    public void RequestExit()
    {
        _exit = true;
        _win.Close();
    }

    private void OnClosing(object? _, System.ComponentModel.CancelEventArgs e)
    {
        if (_exit)
        {
            _icon.Visible = false;
            return;
        }

        if (App.Settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            _win.Hide();

            // The engine keeps filtering traffic, so the user is told about it once.
            if (!_warnedAboutTray)
            {
                _warnedAboutTray = true;
                try
                {
                    _icon.BalloonTipTitle = Strings.T("AppTitle");
                    _icon.BalloonTipText = Strings.T("TrayStillProtecting");
                    _icon.BalloonTipIcon = WF.ToolTipIcon.Info;
                    _icon.ShowBalloonTip(6000);
                }
                catch { }
            }
        }
        else
        {
            _icon.Visible = false;
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

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

    public void RefreshLanguage()
    {
        try
        {
            _icon.Text = SafeTrayText(Strings.T("TrayTooltip"));
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

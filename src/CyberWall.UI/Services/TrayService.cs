using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Service.Engine;
using CyberWall.UI.Popup;
using CyberWall.UI.Tray;
using WF = System.Windows.Forms;

namespace CyberWall.UI.Services;

public sealed class TrayService : IDisposable
{
    private const string WinFormsWindowClassPrefix = "WindowsForms10.Window";
    private const uint MaxNotifyIconId = 64;

    // The shell can be restarted by Windows while the app is starting (for example after a
    // resume) and then the notification area misses the icon for the whole session. The
    // watchdog re-asserts the icon while the session settles. Later Explorer restarts are
    // already handled by WinForms, which re-adds the icon when it receives TaskbarCreated.
    private static readonly TimeSpan ShellWatchdogInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ShellWatchdogDuration = TimeSpan.FromMinutes(3);

    private readonly WF.NotifyIcon _icon;
    private readonly MainWindow _win;
    private readonly FirewallService _svc;
    private readonly DispatcherTimer _shellWatchdog;
    private readonly Stopwatch _runtime = Stopwatch.StartNew();
    private int _repairAttempts;
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

        _shellWatchdog = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = ShellWatchdogInterval
        };
        _shellWatchdog.Tick += (_, _) => CheckShellRegistration();
        _shellWatchdog.Start();
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

    /// <summary>
    /// Re-asserts the icon while the notification area is still coming up. WinForms ignores a
    /// failed NIM_ADD, so without this a missed registration leaves the app without a tray icon
    /// (and without a way to bring the window back) until the process restarts.
    /// </summary>
    private void CheckShellRegistration()
    {
        if (_exit || _runtime.Elapsed > ShellWatchdogDuration || _repairAttempts >= 10)
        {
            _shellWatchdog.Stop();
            return;
        }

        if (!IsIconRegistered())
        {
            _repairAttempts++;
            ReRegisterIcon();
        }
    }

    /// <summary>
    /// Hides the window to the tray only when the icon is actually present. Otherwise the
    /// window stays visible, because hiding it would leave the app unreachable.
    /// </summary>
    private bool EnsureIconAvailable()
    {
        if (IsIconRegistered())
            return true;

        ReRegisterIcon();

        // The shell registers the icon synchronously, but reading its rectangle right after
        // NIM_ADD can still race with the notification area refresh.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            Thread.Sleep(50);
            if (IsIconRegistered())
                return true;
        }

        return false;
    }

    private void ReRegisterIcon()
    {
        try
        {
            // Toggling Visible makes WinForms send NIM_DELETE + NIM_ADD. Setting Visible to
            // true alone would only send NIM_MODIFY, which fails when the shell never accepted
            // the original NIM_ADD.
            _icon.Visible = false;
            _icon.Visible = true;
            UpdateState();
        }
        catch { }
    }

    private static bool IsIconRegistered()
    {
        try
        {
            var processId = Environment.ProcessId;
            var registered = false;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out var windowProcessId);
                if (windowProcessId != (uint)processId)
                    return true;

                if (!GetWindowClassName(hWnd).StartsWith(WinFormsWindowClassPrefix, StringComparison.Ordinal))
                    return true;

                for (uint id = 1; id <= MaxNotifyIconId; id++)
                {
                    var identifier = new NotifyIconIdentifier
                    {
                        Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                        Window = hWnd,
                        Id = id,
                        Item = Guid.Empty
                    };

                    // S_OK means the icon is visible; S_FALSE means it is registered but
                    // stored in the overflow flyout. Both are success codes, so any failure
                    // HRESULT means the icon is really missing from the notification area.
                    if (Shell_NotifyIconGetRect(ref identifier, out _) >= 0)
                    {
                        registered = true;
                        return false;
                    }
                }

                return true;
            }, IntPtr.Zero);

            // Our own NotifyIcon always creates its window, so an empty list also means the
            // icon is not registered.
            return registered;
        }
        catch
        {
            // Probing must never block normal behaviour: assume the icon is fine when unknown.
            return true;
        }
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        var length = GetClassName(hWnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    public void RequestExit()
    {
        _exit = true;
        _shellWatchdog.Stop();
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

            if (!EnsureIconAvailable())
            {
                // Hiding the window without a tray icon would leave the app unreachable, so it
                // stays visible and the user is told why.
                _win.Show();
                if (_win.WindowState == WindowState.Minimized)
                    _win.WindowState = WindowState.Normal;
                _win.Activate();

                AppInfoToast.ShowToast(
                    Strings.T("TrayUnavailableTitle"),
                    Strings.T("TrayUnavailableDesc"),
                    badgeType: ToastBadgeType.Warning);
                return;
            }

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
        _exit = true;
        _shellWatchdog.Stop();
        _icon.Visible = false;
        _icon.Dispose();
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Item;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out WindowRect rect);
}

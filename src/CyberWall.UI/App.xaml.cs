using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\CyberWall_SingleInstance_Mutex";
    public const string ShowWindowMessageName = "CyberWall_RestoreMainWindowMessage";
    public static readonly uint WM_SHOW_MAIN_WINDOW = RegisterWindowMessage(ShowWindowMessageName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        bool isStartingMinimized = e.Args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            if (!isStartingMinimized)
            {
                PostMessage(HWND_BROADCAST, WM_SHOW_MAIN_WINDOW, IntPtr.Zero, IntPtr.Zero);
            }
            Shutdown();
            return;
        }

        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
        }
        catch { }

        Settings = AppSettings.Load();
        Strings.Current = Settings.Language;
        ThemeManager.Apply(Settings.Theme);
        try
        {
            CyberWall.Service.Wfp.RealFirewall.EnsureSelfAllowed();
        }
        catch { }

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        if (isStartingMinimized)
        {
            mainWindow.CheckForUpdatesOnStartup();
        }
        else
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }
        base.OnExit(e);
    }
}

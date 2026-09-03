using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Settings = AppSettings.Load();

        bool isStartingMinimized = e.Args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

        // Safety fallback: If launched by Windows Task Scheduler at boot/logon without args,
        // respect the user's StartMinimized preference so the window does not pop up unexpectedly.
        if (!isStartingMinimized && Settings.StartMinimized && IsLaunchedByTaskScheduler())
        {
            isStartingMinimized = true;
        }

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

        Strings.Current = Settings.Language;
        ThemeManager.Apply(Settings.Theme);

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

        // Asynchronously self-heal / synchronize the scheduled task to ensure
        // Normal priority (no delayed start) and the correct --minimized arguments.
        Task.Run(() =>
        {
            try
            {
                if (Settings.RunAtStartup || StartupHelper.IsStartupEnabled())
                {
                    StartupHelper.EnsureTaskConfigured(Settings.StartMinimized);
                }
            }
            catch { }
        });
    }

    private static bool IsLaunchedByTaskScheduler()
    {
        try
        {
            uint parentPid = GetParentProcessId();
            if (parentPid == 0) return false;

            using var proc = Process.GetProcessById((int)parentPid);
            var name = proc.ProcessName;
            return name.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("taskhostw", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("taskhost", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static uint GetParentProcessId()
    {
        uint currentPid = (uint)Process.GetCurrentProcess().Id;
        IntPtr snapshot = CreateToolhelp32Snapshot(0x00000002 /* TH32CS_SNAPPROCESS */, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return 0;

        try
        {
            var pe = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref pe))
            {
                do
                {
                    if (pe.th32ProcessID == currentPid)
                        return pe.th32ParentProcessID;
                }
                while (Process32Next(snapshot, ref pe));
            }
            return 0;
        }
        finally
        {
            CloseHandle(snapshot);
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

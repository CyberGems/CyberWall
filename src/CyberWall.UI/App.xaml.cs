using System.Runtime.InteropServices;
using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
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
        base.OnStartup(e);
    }
}

using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Settings = AppSettings.Load();
        Strings.Current = Settings.Language;
        ThemeManager.Apply(Settings.Theme);
        base.OnStartup(e);
    }
}

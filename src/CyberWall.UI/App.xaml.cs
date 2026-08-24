using System.Windows;
using CyberWall.Common.I18n;

namespace CyberWall.UI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Strings.Current = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        base.OnStartup(e);
    }
}

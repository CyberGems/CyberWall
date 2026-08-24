using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Controls;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _s;
    private bool _loading;

    public SettingsWindow(AppSettings s)
    {
        InitializeComponent();
        _s = s;
        _loading = true;
        LangBox.SelectedIndex = s.Language == Lang.Es ? 0 : 1;

        switch (s.Theme)
        {
            case AppTheme.CyberWall:
                CyberWallCard.IsChecked = true;
                break;
            case AppTheme.Dark:
                DarkCard.IsChecked = true;
                break;
            case AppTheme.Light:
                LightCard.IsChecked = true;
                break;
        }

        UpdateTexts();
        _loading = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void UpdateTexts()
    {
        var es = _s.Language == Lang.Es;
        Title = es ? "Configuración" : "Settings";
        TitleLbl.Text = es ? "Configuración" : "Settings";
        LangLbl.Text = es ? "Idioma de la interfaz" : "Interface Language";
        ThemeLbl.Text = es ? "Tema de la aplicación" : "Application Theme";
        ThemeSubLbl.Text = es ? "Elige el aspecto visual característico de CyberWall." : "Choose the signature visual appearance of CyberWall.";

        CyberWallCard.RefreshCaption("CyberWall");
        DarkCard.RefreshCaption(es ? "Oscuro" : "Dark");
        LightCard.RefreshCaption(es ? "Claro" : "Light");

        InstantChangeLbl.Text = es ? "Se aplica al instante sin necesidad de reiniciar la aplicación." : "Applied instantly without needing to restart the app.";
        CloseBtn.Content = es ? "Cerrar" : "Close";
    }

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _s.Language = LangBox.SelectedIndex == 0 ? Lang.Es : Lang.En;
        Strings.Current = _s.Language;
        _s.Save();
        UpdateTexts();
        if (Owner is MainWindow mw)
        {
            mw.RefreshLanguage();
        }
    }

    private void ThemeCard_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is ThemeCard card)
        {
            _s.Theme = card.ThemeMode;
            ThemeManager.Apply(_s.Theme);
            _s.Save();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

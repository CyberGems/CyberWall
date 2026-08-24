using System.Windows;
using System.Windows.Controls;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
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
        ThemeBox.SelectedIndex = (int)s.Theme;
        UpdateTexts();
        _loading = false;
    }

    private void UpdateTexts()
    {
        var es = _s.Language == Lang.Es;
        TitleLbl.Text = es ? "Configuración" : "Settings";
        LangLbl.Text = es ? "Idioma" : "Language";
        ThemeLbl.Text = es ? "Tema" : "Theme";
        Title = es ? "Configuración" : "Settings";
        CloseBtn.Content = es ? "Cerrar" : "Close";
    }

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _s.Language = LangBox.SelectedIndex == 0 ? Lang.Es : Lang.En;
        Strings.Current = _s.Language;
        _s.Save();
        UpdateTexts();
        if (Owner is MainWindow mw) mw.RefreshLanguage();
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _s.Theme = (AppTheme)ThemeBox.SelectedIndex;
        ThemeManager.Apply(_s.Theme);
        _s.Save();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Controls;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Popup;
using CyberWall.UI.Services;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CyberWall.UI;

public partial class SettingsWindow : Window, IModalAttentionWindow
{
    private readonly AppSettings _s;
    private bool _loading;
    private DateTime _lastAttentionTime = DateTime.MinValue;

    public SettingsWindow(AppSettings s)
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);
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

        SelectPositionUi(s.NotificationPosition);
        PopulateMonitors();
        SoundToggle.IsChecked = s.PlaySoundOnPrompt;
        StartupToggle.IsChecked = StartupHelper.IsStartupEnabled();
        StartMinimizedToggle.IsChecked = s.StartMinimized;
        MinimizeToTrayToggle.IsChecked = s.MinimizeToTrayOnClose;
        AutoBlockToggle.IsChecked = s.PopupAutoBlockEnabled;
        PopulateAutoBlockWait();
        UpdateTexts();
        Closing += (_, _) => PromptManager.Instance.DismissPreview();
        _loading = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SelectPositionUi(PopupPosition position)
    {
        var accentBrush = (WpfBrush)FindResource("AccentBrush");
        var activeBg = (WpfBrush)FindResource("CardSecondaryBrush");
        var defaultBorderBrush = WpfBrushes.Transparent;
        var defaultBg = WpfBrushes.Transparent;

        var dotActiveBrush = accentBrush;
        var dotInactiveBrush = new SolidColorBrush(WpfColor.FromArgb(64, 128, 128, 128));

        var blocks = new[]
        {
            (PosBlock_TopLeft, PosDot_TopLeft, PopupPosition.TopLeft),
            (PosBlock_TopCenter, PosDot_TopCenter, PopupPosition.TopCenter),
            (PosBlock_TopRight, PosDot_TopRight, PopupPosition.TopRight),
            (PosBlock_Left, PosDot_Left, PopupPosition.Left),
            (PosBlock_Right, PosDot_Right, PopupPosition.Right),
            (PosBlock_BottomLeft, PosDot_BottomLeft, PopupPosition.BottomLeft),
            (PosBlock_BottomCenter, PosDot_BottomCenter, PopupPosition.BottomCenter),
            (PosBlock_BottomRight, PosDot_BottomRight, PopupPosition.BottomRight)
        };

        foreach (var (block, dot, pos) in blocks)
        {
            if (block is null || dot is null) continue;
            if (pos == position)
            {
                block.BorderBrush = accentBrush;
                block.Background = activeBg;
                dot.Background = dotActiveBrush;
            }
            else
            {
                block.BorderBrush = defaultBorderBrush;
                block.Background = defaultBg;
                dot.Background = dotInactiveBrush;
            }
        }
    }

    private void PositionBlock_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string tag) return;
        if (!Enum.TryParse<PopupPosition>(tag, out var selected)) return;

        _s.NotificationPosition = selected;
        _s.Save();
        SelectPositionUi(selected);
        TriggerPreviewPopup();
    }

    private void PopulateMonitors()
    {
        _loading = true;
        MonBox.Items.Clear();
        MonBox.Items.Add(new ComboBoxItem { Content = Strings.T("AutomaticMonitor"), Tag = -1 });

        var screens = PopupWindowHelper.GetSortedScreens();
        for (int i = 0; i < screens.Length; i++)
        {
            var scr = screens[i];
            var label = scr.Primary
                ? $"{Strings.T("PrimaryMonitor")} ({scr.Bounds.Width}x{scr.Bounds.Height})"
                : $"{string.Format(Strings.T("MonitorN"), i + 1)} ({scr.Bounds.Width}x{scr.Bounds.Height})";

            MonBox.Items.Add(new ComboBoxItem { Content = label, Tag = i });
        }

        int targetIndex = 0;
        for (int i = 0; i < MonBox.Items.Count; i++)
        {
            if (MonBox.Items[i] is ComboBoxItem cbi && cbi.Tag is int tag && tag == _s.NotificationMonitor)
            {
                targetIndex = i;
                break;
            }
        }
        MonBox.SelectedIndex = targetIndex;
        _loading = false;
    }

    private void UpdateStartupTogglesState()
    {
        var startupOn = StartupToggle.IsChecked == true;
        StartMinimizedRow.IsEnabled = startupOn;
        StartMinimizedRow.Opacity = startupOn ? 1.0 : 0.45;
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
        LocationHdrLbl.Text = Strings.T("LocationSection");
        PosTitleLbl.Text = Strings.T("NotificationPosition");
        PosDescLbl.Text = Strings.T("PosDesc");
        MonTitleLbl.Text = Strings.T("NotificationMonitor");
        MonDescLbl.Text = Strings.T("MonDesc");
        SoundTitleLbl.Text = Strings.T("PromptSoundToggle");
        SoundDescLbl.Text = Strings.T("PromptSoundDesc");
        BrowseSoundBtn.Content = Strings.T("BrowseSound");
        ResetSoundBtn.Content = Strings.T("ResetSound");
        PreviewSoundBtn.ToolTip = Strings.T("PreviewSound");
        TestTitleLbl.Text = Strings.T("TestNotification");
        TestDescLbl.Text = Strings.T("TestNotifDesc");
        SystemHdrLbl.Text = Strings.T("SystemHeader");
        StartupTitleLbl.Text = Strings.T("RunAtStartup");
        StartupDescLbl.Text = Strings.T("RunAtStartupDesc");
        StartMinimizedTitleLbl.Text = Strings.T("StartMinimized");
        StartMinimizedDescLbl.Text = Strings.T("StartMinimizedDesc");
        MinimizeToTrayTitleLbl.Text = Strings.T("MinimizeToTrayOnClose");
        MinimizeToTrayDescLbl.Text = Strings.T("MinimizeToTrayOnCloseDesc");
        AutoBlockWaitLbl.Text = Strings.T("PopupAutoBlockWait");
        ClearAllTitleLbl.Text = Strings.T("ClearAllRules");
        ClearAllDescLbl.Text = Strings.T("ClearAllRulesDesc");
        ClearAllBtn.Content = Strings.T("ClearAllRulesShort");
        PreviewBtn.Content = Strings.T("PreviewPopup");

        UpdateSoundUiState();
        UpdateStartupTogglesState();
        PopulateMonitors();
        PopulateAutoBlockWait();
    }

    private void UpdateSoundUiState()
    {
        var soundOn = _s.PlaySoundOnPrompt;
        SoundToggle.IsChecked = soundOn;
        CustomSoundRow.IsEnabled = soundOn;
        CustomSoundRow.Opacity = soundOn ? 1.0 : 0.45;
        if (string.IsNullOrWhiteSpace(_s.CustomSoundPath))
        {
            SoundPathDisplay.Text = Strings.T("DefaultSound");
            ResetSoundBtn.Visibility = Visibility.Collapsed;
        }
        else
        {
            SoundPathDisplay.Text = System.IO.Path.GetFileName(_s.CustomSoundPath);
            ResetSoundBtn.Visibility = Visibility.Visible;
        }
    }

    private void SoundToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.PlaySoundOnPrompt = SoundToggle.IsChecked == true;
        _s.Save();
        UpdateSoundUiState();
    }

    private void BrowseSound_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.T("CustomSound"),
            Filter = Strings.T("SoundFilter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            _s.CustomSoundPath = dlg.FileName;
            _s.Save();
            UpdateSoundUiState();
            PromptSoundService.PreviewSound(_s.CustomSoundPath);
        }
    }

    private void ResetSound_Click(object sender, RoutedEventArgs e)
    {
        _s.CustomSoundPath = null;
        _s.Save();
        UpdateSoundUiState();
        PromptSoundService.PreviewSound(null);
    }

    private void PreviewSound_Click(object sender, RoutedEventArgs e)
    {
        PromptSoundService.PreviewSound(_s.CustomSoundPath);
    }

    private void ClearAllRules_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ConfirmDialog(Strings.T("ClearAllRules"), Strings.T("ClearAllRulesConfirm")) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (Owner is MainWindow mw)
            {
                mw.ClearAllRulesFromSettings();
            }
        }
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = StartupToggle.IsChecked == true;
        _s.RunAtStartup = enabled;
        StartupHelper.SetStartupEnabled(enabled, _s.StartMinimized);
        _s.Save();
        UpdateStartupTogglesState();
    }

    private void StartMinimizedToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.StartMinimized = StartMinimizedToggle.IsChecked == true;
        _s.Save();
        if (StartupToggle.IsChecked == true)
        {
            StartupHelper.SetStartupEnabled(true, _s.StartMinimized);
        }
    }

    private void MinimizeToTrayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.MinimizeToTrayOnClose = MinimizeToTrayToggle.IsChecked == true;
        _s.Save();
        if (Owner is MainWindow mw)
        {
            mw.UpdateCloseButtonTooltip();
        }
    }

    private static readonly (int Seconds, string Key)[] AutoBlockWaitOptions =
    {
        (30, "PopupAutoBlock30s"),
        (60, "PopupAutoBlock1m"),
        (120, "PopupAutoBlock2m"),
        (300, "PopupAutoBlock5m"),
        (600, "PopupAutoBlock10m"),
        (900, "PopupAutoBlock15m"),
        (1800, "PopupAutoBlock30m")
    };

    private void PopulateAutoBlockWait()
    {
        var wasLoading = _loading;
        _loading = true;
        AutoBlockWaitBox.Items.Clear();
        foreach (var (secs, key) in AutoBlockWaitOptions)
            AutoBlockWaitBox.Items.Add(new ComboBoxItem { Content = Strings.T(key), Tag = secs });

        var want = _s.PopupAutoBlockSeconds;
        int best = 3;
        int bestDiff = int.MaxValue;
        for (int i = 0; i < AutoBlockWaitBox.Items.Count; i++)
        {
            if (AutoBlockWaitBox.Items[i] is not ComboBoxItem { Tag: int secs }) continue;
            var d = Math.Abs(secs - want);
            if (d >= bestDiff) continue;
            bestDiff = d;
            best = i;
        }
        AutoBlockWaitBox.SelectedIndex = best;
        AutoBlockWaitRow.IsEnabled = _s.PopupAutoBlockEnabled;
        AutoBlockWaitRow.Opacity = _s.PopupAutoBlockEnabled ? 1 : 0.45;
        _loading = wasLoading;
    }

    private void AutoBlockToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.PopupAutoBlockEnabled = AutoBlockToggle.IsChecked == true;
        _s.Save();
        AutoBlockWaitRow.IsEnabled = _s.PopupAutoBlockEnabled;
        AutoBlockWaitRow.Opacity = _s.PopupAutoBlockEnabled ? 1 : 0.45;
    }

    private void AutoBlockWaitBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (AutoBlockWaitBox.SelectedItem is ComboBoxItem { Tag: int secs })
        {
            _s.PopupAutoBlockSeconds = secs;
            _s.Save();
        }
    }

    private void TriggerPreviewPopup()
    {
        PromptManager.Instance.ShowPreview(_s.NotificationPosition, _s.NotificationMonitor);
    }

    private void PreviewBtn_Click(object sender, RoutedEventArgs e)
    {
        TriggerPreviewPopup();
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
            SelectPositionUi(_s.NotificationPosition);
        }
    }

    private void MonBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (MonBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is int mon)
        {
            _s.NotificationMonitor = mon;
            _s.Save();
            TriggerPreviewPopup();
        }
    }

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;
using CyberWall.UI.Services;

namespace CyberWall.UI.Popup;

public enum ToastBadgeType
{
    Info,
    Success,
    Warning,
    Danger
}

public partial class AppInfoToast : Window
{
    private static readonly List<AppInfoToast> _active = new();
    private static readonly PathToIconConverter IconConv = new();
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly string? _appPath;

    public AppInfoToast(string title, string message, string? appPath = null, ToastBadgeType badgeType = ToastBadgeType.Info, string? badgeText = null)
    {
        _appPath = appPath;
        InitializeComponent();

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) =>
        {
            UpdateLayout();
            PopupWindowHelper.PositionWindow(this, App.Settings.NotificationPosition, App.Settings.NotificationMonitor);
        };

        TitleLbl.Text = string.IsNullOrWhiteSpace(title) ? Strings.T("AppInfoMonitor") : title;
        DescLbl.Text = message;
        CloseBtn.ToolTip = Strings.T("Close");
        AutomationProperties.SetName(CloseBtn, Strings.T("Close"));
        SettingsBtn.ToolTip = Strings.T("Settings");
        AutomationProperties.SetName(SettingsBtn, Strings.T("Settings"));

        ApplyBadgeStyle(badgeType, badgeText);
        ApplyIcon(appPath, badgeType);

        if (!string.IsNullOrWhiteSpace(_appPath))
        {
            ProcessActionsPanel.Visibility = Visibility.Visible;
            PathLbl.Text = Path.GetFileName(_appPath);
            PathLbl.ToolTip = _appPath;
            SearchBtn.ToolTip = Strings.T("SearchOnline");
            CopyPathBtn.ToolTip = Strings.T("CopyPath");
            OpenFolderBtn.ToolTip = Strings.T("OpenFolder");
        }
        else
        {
            ProcessActionsPanel.Visibility = Visibility.Collapsed;
        }

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            CloseToast();
        };
        _autoCloseTimer.Start();

        MouseEnter += (_, _) => _autoCloseTimer.Stop();
        MouseLeave += (_, _) =>
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Start();
        };
        MouseLeftButtonDown += OnBodyClick;
    }

    private void ApplyBadgeStyle(ToastBadgeType type, string? customBadge)
    {
        switch (type)
        {
            case ToastBadgeType.Success:
                BadgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeAllowBgBrush");
                BadgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeAllowFgBrush");
                BadgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeAllowFgBrush");
                BadgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "ACTIVO" : "ONLINE");
                break;
            case ToastBadgeType.Danger:
                BadgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeBlockBgBrush");
                BadgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeBlockFgBrush");
                BadgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeBlockFgBrush");
                BadgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "ALERTA" : "OFFLINE");
                break;
            case ToastBadgeType.Warning:
                BadgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeWarnBgBrush");
                BadgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeWarnFgBrush");
                BadgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeWarnFgBrush");
                BadgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "AVISO" : "WARN");
                break;
            case ToastBadgeType.Info:
            default:
                BadgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeWarnBgBrush");
                BadgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeWarnFgBrush");
                BadgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeWarnFgBrush");
                BadgeLbl.Text = customBadge ?? "INFO";
                break;
        }
    }

    private void ApplyIcon(string? appPath, ToastBadgeType type)
    {
        AppIconImg.Visibility = Visibility.Collapsed;
        WifiOffIcon.Visibility = Visibility.Collapsed;
        WifiOnIcon.Visibility = Visibility.Collapsed;
        ShieldWarnIcon.Visibility = Visibility.Collapsed;
        ShieldSuccessIcon.Visibility = Visibility.Collapsed;
        InfoIcon.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(appPath))
        {
            var icon = IconConv.Convert(appPath, typeof(ImageSource), null!, null!) as ImageSource;
            if (icon != null)
            {
                AppIconImg.Source = icon;
                AppIconImg.Visibility = Visibility.Visible;
                return;
            }
        }

        switch (type)
        {
            case ToastBadgeType.Danger:
                WifiOffIcon.Visibility = Visibility.Visible;
                break;
            case ToastBadgeType.Success:
                WifiOnIcon.Visibility = Visibility.Visible;
                break;
            case ToastBadgeType.Warning:
                ShieldWarnIcon.Visibility = Visibility.Visible;
                break;
            default:
                InfoIcon.Visibility = Visibility.Visible;
                break;
        }
    }

    public static void ShowToast(string title, string message, string? appPath = null, ToastBadgeType badgeType = ToastBadgeType.Info, string? badgeText = null)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (PopupWindowHelper.HasOpenPermissionPopup()) return;
            var toast = new AppInfoToast(title, message, appPath, badgeType, badgeText);
            _active.Add(toast);
            toast.Show();
        });
    }

    public static void CloseAll()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(() =>
        {
            foreach (var toast in _active.ToList())
                toast.CloseToast();
        });
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null) return;
        _autoCloseTimer.Stop();
        CloseToast();
        OpenNotifications();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        CloseToast();
        if (System.Windows.Application.Current.MainWindow is MainWindow mw)
        {
            mw.Dispatcher.Invoke(() => mw.OpenSettings());
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_appPath)) return;
        try
        {
            var fn = Path.GetFileName(_appPath);
            Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString($"{fn} process"))
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_appPath)) return;
        try
        {
            System.Windows.Clipboard.SetText(_appPath);
            CopyPathBtn.ToolTip = Strings.T("CopiedToClipboard");
        }
        catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_appPath)) return;
        try
        {
            if (File.Exists(_appPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_appPath}\"") { UseShellExecute = true });
                return;
            }
            var dir = Path.GetDirectoryName(_appPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        CloseToast();
    }

    private void CloseToast()
    {
        _active.Remove(this);
        try { Close(); } catch { }
    }

    private static void OpenNotifications()
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow mw) return;
        mw.ShowNotifications();
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T target) return target;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;
using CyberWall.UI.Services;

namespace CyberWall.UI.Popup;

public partial class AppInfoToast : Window
{
    private static readonly List<AppInfoToast> _active = new();
    private static readonly PathToIconConverter IconConv = new();
    private readonly DispatcherTimer _autoCloseTimer;

    public AppInfoToast(string title, string message, string? appPath)
    {
        InitializeComponent();

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) =>
        {
            UpdateLayout();
            PopupWindowHelper.PositionWindow(this, App.Settings.NotificationPosition, App.Settings.NotificationMonitor);
        };

        TitleLbl.Text = string.IsNullOrWhiteSpace(title) ? Strings.T("AppInfoMonitor") : title;
        DescLbl.Text = message;
        BadgeLbl.Text = "INFO";
        CloseBtn.ToolTip = Strings.T("Close");
        AutomationProperties.SetName(CloseBtn, Strings.T("Close"));

        if (!string.IsNullOrWhiteSpace(appPath))
        {
            var icon = IconConv.Convert(appPath, typeof(ImageSource), null!, null!) as ImageSource;
            if (icon != null) AppIconImg.Source = icon;
        }

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
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

    public static void ShowToast(string title, string message, string? appPath)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (PopupWindowHelper.HasOpenPermissionPopup()) return;
            var toast = new AppInfoToast(title, message, appPath);
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
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        _autoCloseTimer.Stop();
        CloseToast();
        OpenNotifications();
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
}

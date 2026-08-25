using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;

namespace CyberWall.UI.Popup;

public partial class AutoBlockToast : Window
{
    private static readonly List<AutoBlockToast> _active = new();
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly Action? _onUndo;

    public ConnectionEvent Event { get; }

    public AutoBlockToast(ConnectionEvent ev, Action? onUndo)
    {
        InitializeComponent();
        Event = ev;
        _onUndo = onUndo;
        DataContext = this;

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) =>
        {
            UpdateLayout();
            PopupWindowHelper.PositionWindow(this, App.Settings.NotificationPosition, App.Settings.NotificationMonitor);
        };

        var name = ev.DisplayName;
        TitleLbl.Text = Strings.T("AutoBlockedTitle");
        BadgeLbl.Text = Strings.T("AutoBlockedBadge");
        DescLbl.Text = Strings.T("AutoBlockedDesc", name);
        UndoBtn.Content = Strings.T("AutoBlockedUndo");
        CloseBtn.ToolTip = Strings.T("Close");
        AutomationProperties.SetName(CloseBtn, Strings.T("Close"));
        AutomationProperties.SetName(UndoBtn, Strings.T("AutoBlockedUndo"));

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
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

    public static void ShowToast(ConnectionEvent ev, Action? onUndo = null)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (PopupWindowHelper.HasOpenPermissionPopup()) return;
            var toast = new AutoBlockToast(ev, onUndo);
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
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        _autoCloseTimer.Stop();
        CloseToast();
        OpenNotifications();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        try { _onUndo?.Invoke(); } catch { }
        CloseToast();
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

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}

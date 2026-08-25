using System.Windows;
using System.Windows.Input;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.UI.Services;

namespace CyberWall.UI.Dialogs;

public partial class NotificationsDialog : Window
{
    private readonly NotificationStore _store;
    private readonly HashSet<Guid> _openedUnread;
    private readonly Action<string> _onAllow;
    private readonly Action _onEnableProtection;
    private readonly Action _onDownloadUpdate;

    public NotificationsDialog(NotificationStore store, Action<string> onAllow, Action onEnableProtection, Action onDownloadUpdate)
    {
        _store = store;
        _onAllow = onAllow;
        _onEnableProtection = onEnableProtection;
        _onDownloadUpdate = onDownloadUpdate;
        _openedUnread = store.All.Where(n => !n.Read).Select(n => n.Id).ToHashSet();
        store.MarkAllRead();

        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        CyberWallWindowChrome.Apply(this, 12);
        RefreshLanguage();
        BindList();
        _store.Changed += OnStoreChanged;
        Closed += (_, _) => _store.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged() => Dispatcher.BeginInvoke(BindList);

    private void RefreshLanguage()
    {
        TitleLbl.Text = Strings.T("Notifications");
        MarkReadBtnText.Text = Strings.T("MarkAllRead");
        ClearBtnText.Text = Strings.T("ClearNotifications");
        CloseBtn.Content = Strings.T("Close");
        EmptyMsg.Text = Strings.T("NotificationsEmpty");
    }

    private void BindList()
    {
        var items = _store.All.Select(n => NotificationItemVm.From(n, _openedUnread.Contains(n.Id))).ToList();
        NotifList.ItemsSource = items;
        CountBadge.Text = _openedUnread.Count > 0
            ? Strings.T("NotifUnread", _openedUnread.Count)
            : Strings.T("NotifCount", items.Count);
        EmptyMsg.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NotifList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NotificationItemVm vm }) return;
        switch (vm.Kind)
        {
            case AppNotificationKind.AutoBlocked:
            case AppNotificationKind.SilentBlock:
                if (string.IsNullOrWhiteSpace(vm.AppPath)) return;
                _onAllow(vm.AppPath);
                _store.MarkRelatedRead(vm.Kind, vm.AppPath);
                _openedUnread.Remove(vm.Id);
                BindList();
                break;
            case AppNotificationKind.ProtectionOff:
                _onEnableProtection();
                _store.MarkRelatedRead(AppNotificationKind.ProtectionOff, null);
                BindList();
                break;
            case AppNotificationKind.UpdateAvailable:
                Close();
                _onDownloadUpdate();
                break;
        }
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        _openedUnread.Clear();
        _store.MarkAllRead();
        BindList();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Show(this, Strings.T("ClearNotifications"), Strings.T("ClearNotificationsConfirm"), Strings.T("Ok"), Strings.T("Cancel")))
            return;
        _openedUnread.Clear();
        _store.Clear();
        BindList();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

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
    private readonly Func<string, Task> _onAllow;
    private readonly Action _onEnableProtection;
    private readonly Action _onDownloadUpdate;
    private bool _busy;
    public bool OpenSettingsAfterClose { get; private set; }

    public NotificationsDialog(NotificationStore store, Func<string, Task> onAllow, Action onEnableProtection, Action onDownloadUpdate)
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

    private void OnStoreChanged()
    {
        if (_busy) return;
        Dispatcher.BeginInvoke(BindList);
    }

    private void RefreshLanguage()
    {
        TitleLbl.Text = Strings.T("Notifications");
        MarkReadBtnText.Text = Strings.T("MarkAllRead");
        ClearBtnText.Text = Strings.T("ClearNotifications");
        CloseBtn.Content = Strings.T("Close");
        EmptyMsg.Text = Strings.T("NotificationsEmpty");
        SettingsTitleBtn.ToolTip = Strings.T("Settings");
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

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NotificationItemVm vm }) return;
        if (!vm.CanAct) return;

        switch (vm.Kind)
        {
            case AppNotificationKind.AutoBlocked:
            case AppNotificationKind.SilentBlock:
                await AllowAsync(vm);
                break;
            case AppNotificationKind.ProtectionOff:
                await EnableAsync(vm);
                break;
            case AppNotificationKind.UpdateAvailable:
                Close();
                _onDownloadUpdate();
                break;
        }
    }

    private async Task AllowAsync(NotificationItemVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.AppPath)) return;
        _busy = true;
        vm.MarkBusy(Strings.T("NotifAllowing"));
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        try
        {
            try
            {
                await _onAllow(vm.AppPath);
            }
            catch
            {
                vm.IsBusy = false;
                vm.ActionLabel = Strings.T("AutoBlockedUndo");
                return;
            }
            var name = string.IsNullOrWhiteSpace(vm.AppName) ? "?" : vm.AppName;
            vm.MarkResolved(Strings.T("NotifAllowed"), Strings.T("NotifAllowedDesc", name));
            await Task.Delay(900);
            _store.Remove(vm.Id);
            _openedUnread.Remove(vm.Id);
        }
        finally
        {
            _busy = false;
            BindList();
        }
    }

    private async Task EnableAsync(NotificationItemVm vm)
    {
        _busy = true;
        vm.MarkBusy(Strings.T("NotifAllowing"));
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        try
        {
            _onEnableProtection();
            vm.MarkResolved(Strings.T("NotifProtectionOn"), Strings.T("NotifProtectionOnDesc"));
            await Task.Delay(700);
            _store.Remove(vm.Id);
            _openedUnread.Remove(vm.Id);
        }
        finally
        {
            _busy = false;
            BindList();
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

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsAfterClose = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

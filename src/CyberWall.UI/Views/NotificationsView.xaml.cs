using System.Windows;
using System.Windows.Automation;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace CyberWall.UI.Views;

public partial class NotificationsView : UserControl
{
    private NotificationStore? _store;
    private Func<string, Task>? _onAllow;
    private Func<string, Task>? _onBlock;
    private Action? _onEnableProtection;
    private Action? _onDownloadUpdate;
    private Action? _onOpenSettings;
    private Action? _onBeforeActivate;
    private Func<bool>? _isProtectionOn;
    private Func<bool>? _isOnline;
    private HashSet<Guid> _openedUnread = new();
    private bool _busy;
    private bool _subscribed;

    public event EventHandler? BackRequested;

    public NotificationsView()
    {
        InitializeComponent();
        Unloaded += (_, _) => Unsubscribe();
    }

    public void Initialize(
        NotificationStore store,
        Func<string, Task> onAllow,
        Func<string, Task> onBlock,
        Action onEnableProtection,
        Action onDownloadUpdate,
        Action onOpenSettings,
        Action? onBeforeActivate = null,
        Func<bool>? isProtectionOn = null,
        Func<bool>? isOnline = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _onAllow = onAllow ?? throw new ArgumentNullException(nameof(onAllow));
        _onBlock = onBlock ?? throw new ArgumentNullException(nameof(onBlock));
        _onEnableProtection = onEnableProtection ?? throw new ArgumentNullException(nameof(onEnableProtection));
        _onDownloadUpdate = onDownloadUpdate ?? throw new ArgumentNullException(nameof(onDownloadUpdate));
        _onOpenSettings = onOpenSettings ?? throw new ArgumentNullException(nameof(onOpenSettings));
        _onBeforeActivate = onBeforeActivate;
        _isProtectionOn = isProtectionOn;
        _isOnline = isOnline;
    }

    public void Activate()
    {
        if (_store == null) return;

        _onBeforeActivate?.Invoke();
        _store.PurgeObsoleteUpdateNotifications(UpdateService.GetCurrentVersion());
        if (_isProtectionOn?.Invoke() == true)
        {
            _store.PurgeObsoleteProtectionOffNotifications();
        }

        _openedUnread = _store.All.Where(n => !n.Read).Select(n => n.Id).ToHashSet();
        _store.MarkAllRead();

        Subscribe();
        RefreshLanguage();
        UpdatePausedBanner();
        BindList();
    }

    public void Deactivate()
    {
        Unsubscribe();
        _openedUnread.Clear();
    }

    public void RefreshLanguage()
    {
        TitleLbl.Text = Strings.T("Notifications");
        SubtitleLbl.Text = Strings.T("NotificationsSubtitle");
        MarkReadBtnText.Text = Strings.T("MarkAllRead");
        ClearBtnText.Text = Strings.T("ClearNotifications");
        BackBtnText.Text = Strings.T("Back");
        AutomationProperties.SetName(BackBtn, Strings.T("Back"));
        EmptyMsg.Text = Strings.T("NotificationsEmpty");
        SettingsBtn.ToolTip = Strings.T("Settings");
        AutomationProperties.SetName(SettingsBtn, Strings.T("Settings"));

        UpdatePausedBanner();
        if (_store != null)
        {
            BindList();
        }
    }

    private void Subscribe()
    {
        if (_subscribed || _store == null) return;
        _store.Changed += OnStoreChanged;
        FloatingAlertsController.PauseStateChanged += OnPauseStateChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _store == null) return;
        _store.Changed -= OnStoreChanged;
        FloatingAlertsController.PauseStateChanged -= OnPauseStateChanged;
        _subscribed = false;
    }

    private void OnPauseStateChanged()
    {
        Dispatcher.BeginInvoke(UpdatePausedBanner);
    }

    private void UpdatePausedBanner()
    {
        var desc = FloatingAlertsController.GetPausedBannerDescription(App.Settings);
        if (string.IsNullOrEmpty(desc))
        {
            PausedBanner.Visibility = Visibility.Collapsed;
            return;
        }

        PausedBannerTitle.Text = Strings.T("FloatingAlertsPausedBannerTitle");
        PausedBannerDesc.Text = desc;
        PausedResumeBtn.Content = Strings.T("ResumeNow");
        PausedBanner.Visibility = Visibility.Visible;
    }

    private void PausedResume_Click(object sender, RoutedEventArgs e)
    {
        FloatingAlertsController.Resume();
    }

    private void OnStoreChanged()
    {
        if (_busy) return;
        Dispatcher.BeginInvoke(BindList);
    }

    private void BindList()
    {
        if (_store == null) return;

        bool isProt = _isProtectionOn?.Invoke() ?? false;
        bool isOnline = _isOnline?.Invoke() ?? ConnectivityService.Instance.IsOnline;
        var items = _store.All
            .Select(n => NotificationItemVm.From(n, _openedUnread.Contains(n.Id), isProt, isOnline))
            .ToList();

        NotifList.ItemsSource = items;
        CountBadge.Text = _openedUnread.Count > 0
            ? Strings.T("NotifUnread", _openedUnread.Count)
            : Strings.T("NotifCount", items.Count);

        EmptyStateCard.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NotifList.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ClearBtn.IsEnabled = items.Count > 0;
        MarkReadBtn.IsEnabled = _openedUnread.Count > 0;
    }

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NotificationItemVm vm }) return;
        if (!vm.CanAct) return;

        switch (vm.Kind)
        {
            case AppNotificationKind.AutoBlocked:
            case AppNotificationKind.SilentBlock:
            case AppNotificationKind.PendingDecision:
                await AllowAsync(vm);
                break;
            case AppNotificationKind.ProtectionOff:
                await EnableAsync(vm);
                break;
            case AppNotificationKind.UpdateAvailable:
                _onDownloadUpdate?.Invoke();
                break;
            case AppNotificationKind.InternetLost:
                await CheckInternetAsync(vm);
                break;
        }
    }

    private async Task AllowAsync(NotificationItemVm vm)
    {
        if (_store == null || _onAllow == null || string.IsNullOrWhiteSpace(vm.AppPath)) return;
        _busy = true;
        var originalLabel = vm.ActionLabel;
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
                vm.ActionLabel = originalLabel;
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

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null) return;
        if (sender is not FrameworkElement { Tag: NotificationItemVm vm }) return;
        if (!vm.CanDismiss) return;
        DismissNotification(vm.Id);
    }

    internal bool DismissNotification(Guid id)
    {
        if (_store == null || _busy) return false;
        _store.Remove(id);
        _openedUnread.Remove(id);
        BindList();
        return true;
    }

    private async void Block_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NotificationItemVm vm }) return;
        if (!vm.CanBlock) return;
        await BlockAsync(vm);
    }

    private async Task BlockAsync(NotificationItemVm vm)
    {
        if (_store == null || _onBlock == null || string.IsNullOrWhiteSpace(vm.AppPath)) return;
        _busy = true;
        vm.IsBusy = true;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        try
        {
            try
            {
                await _onBlock(vm.AppPath);
            }
            catch
            {
                vm.IsBusy = false;
                return;
            }
            var name = string.IsNullOrWhiteSpace(vm.AppName) ? "?" : vm.AppName;
            vm.MarkResolved(Strings.T("NotifBlocked"), Strings.T("NotifBlockedDesc", name), blocked: true);
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
        if (_store == null || _onEnableProtection == null) return;
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

    private async Task CheckInternetAsync(NotificationItemVm vm)
    {
        if (_store == null) return;
        _busy = true;
        vm.MarkBusy(Strings.T("NotifInternetChecking"));
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        try
        {
            bool online = await ConnectivityService.Instance.CheckConnectivityAsync(force: true);
            if (online)
            {
                vm.MarkResolved(Strings.T("NotifInternetRestoredTitle"), Strings.T("NotifInternetRestoredDesc"));
                await Task.Delay(900);
                _store.Remove(vm.Id);
                _openedUnread.Remove(vm.Id);
            }
            else
            {
                vm.IsBusy = false;
                vm.ShowAction = true;
                vm.ActionLabel = Strings.T("NotifInternetStillOffline");
                await Task.Delay(1400);
                vm.ActionLabel = Strings.T("NotifInternetRecheck");
            }
        }
        finally
        {
            _busy = false;
            BindList();
        }
    }

    private void MarkAllRead_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null) return;
        _openedUnread.Clear();
        _store.MarkAllRead();
        BindList();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_store == null) return;
        var owner = Window.GetWindow(this);
        if (!ConfirmDialog.Show(owner, Strings.T("ClearNotifications"), Strings.T("ClearNotificationsConfirm"), Strings.T("Ok"), Strings.T("Cancel")))
            return;

        _openedUnread.Clear();
        _store.Clear();
        BindList();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => _onOpenSettings?.Invoke();

    private void BackBtn_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}


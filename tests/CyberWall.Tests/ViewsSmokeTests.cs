using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Views;

namespace CyberWall.Tests;

public class ViewsSmokeTests
{
    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            var prevLang = Strings.Current;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                Strings.Current = prevLang;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    [Fact]
    public void AboutView_ConstructsAndRefreshesLanguagesOnSta()
    {
        RunOnSta(() =>
        {
            Strings.Current = Lang.En;
            var view = new AboutView();
            view.RefreshLanguage();
            Assert.False(string.IsNullOrWhiteSpace(view.AboutTitleText.Text));
            var enTitle = view.AboutTitleText.Text;
            var enDiagTip = Assert.IsType<string>(view.AboutFooterDiagBtn.ToolTip);
            Assert.False(string.IsNullOrWhiteSpace(enDiagTip));

            Strings.Current = Lang.Es;
            view.RefreshLanguage();
            Assert.NotEqual(enTitle, view.AboutTitleText.Text);
            Assert.NotEqual(enDiagTip, Assert.IsType<string>(view.AboutFooterDiagBtn.ToolTip));
        });
    }

    [Fact]
    public void NotificationsView_ActivatesDismissesAndRefreshesLanguagesOnSta()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"cyberwall-notif-view-{Guid.NewGuid():N}.json");
        try
        {
            RunOnSta(() =>
            {
                var store = new NotificationStore(tempPath);
                store.Add(AppNotificationKind.PendingDecision, @"C:\Apps\Demo.exe", "Demo", "1.2.3.4:443");
                store.Add(AppNotificationKind.AutoBlocked, @"C:\Apps\Other.exe", "Other");
                store.Add(AppNotificationKind.ProtectionOff, null, null);
                Assert.Equal(3, store.All.Count);

                var view = new NotificationsView();
                view.Initialize(
                    store,
                    _ => Task.CompletedTask,
                    _ => Task.CompletedTask,
                    () => { },
                    () => { },
                    () => { },
                    isProtectionOn: () => false,
                    isOnline: () => true);

                Strings.Current = Lang.En;
                view.Activate();

                var items = Assert.IsAssignableFrom<IReadOnlyList<NotificationItemVm>>(view.NotifList.ItemsSource);
                Assert.Equal(3, items.Count);
                Assert.All(items, item =>
                {
                    Assert.True(item.CanDismiss);
                    Assert.False(string.IsNullOrWhiteSpace(item.DismissToolTip));
                });

                var enTooltip = items[0].DismissToolTip;
                Strings.Current = Lang.Es;
                view.RefreshLanguage();
                var esItems = Assert.IsAssignableFrom<IReadOnlyList<NotificationItemVm>>(view.NotifList.ItemsSource);
                Assert.NotEqual(enTooltip, esItems[0].DismissToolTip);

                Assert.True(view.DismissNotification(esItems[0].Id));
                Assert.Equal(2, store.All.Count);
                var afterDismiss = Assert.IsAssignableFrom<IReadOnlyList<NotificationItemVm>>(view.NotifList.ItemsSource);
                Assert.Equal(2, afterDismiss.Count);

                view.Deactivate();
                Assert.Equal(Visibility.Visible, view.NotifList.Visibility);
            });
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}


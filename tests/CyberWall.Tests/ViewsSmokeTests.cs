using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.Common.Settings;
using CyberWall.UI.Controls;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Services;
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
                    isProtectionOn: () => false,
                    isOnline: () => true);

                Strings.Current = Lang.En;
                view.Activate();
                Assert.Null(view.FindName("SettingsBtn"));
                Assert.False(string.IsNullOrWhiteSpace(view.SortBtnText.Text));
                var enSortLabel = view.SortBtnText.Text;

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
                Assert.NotEqual(enSortLabel, view.SortBtnText.Text);
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

    [Fact]
    public void NotificationsView_SortNotificationsOrdersByDateTypeAndApps()
    {
        var t0 = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            new AppNotification
            {
                Kind = AppNotificationKind.AutoBlocked,
                AppPath = @"C:\Apps\Zeta.exe",
                AppName = "Zeta",
                Timestamp = t0.AddMinutes(3),
                Read = true
            },
            new AppNotification
            {
                Kind = AppNotificationKind.ProtectionOff,
                Timestamp = t0.AddMinutes(2)
            },
            new AppNotification
            {
                Kind = AppNotificationKind.PendingDecision,
                AppPath = @"C:\Apps\Alpha.exe",
                AppName = "Alpha",
                Detail = "1.1.1.1:443",
                Timestamp = t0.AddMinutes(1)
            }
        };

        var byDate = NotificationsView.SortNotifications(items, NotificationSortBy.Date);
        Assert.Equal([AppNotificationKind.AutoBlocked, AppNotificationKind.ProtectionOff, AppNotificationKind.PendingDecision],
            byDate.Select(n => n.Kind).ToArray());

        var byType = NotificationsView.SortNotifications(items, NotificationSortBy.Type);
        Assert.Equal([AppNotificationKind.PendingDecision, AppNotificationKind.ProtectionOff, AppNotificationKind.AutoBlocked],
            byType.Select(n => n.Kind).ToArray());

        var byApps = NotificationsView.SortNotifications(items, NotificationSortBy.Apps);
        Assert.Equal(3, byApps.Count);
        Assert.Equal("Alpha", byApps[0].AppName);
        Assert.Equal("Zeta", byApps[1].AppName);
        Assert.Null(byApps[2].AppName);
    }

    [Fact]
    public void AboutView_InlineUpdateBannerAndRecommendationsVisibilityWorkOnSta()
    {
        RunOnSta(() =>
        {
            var settings = new AppSettings();
            Strings.Current = Lang.En;
            var view = new AboutView(settings);
            view.Activate();

            Assert.Equal(Visibility.Visible, view.SuiteSection.Visibility);
            Assert.Equal(Visibility.Collapsed, view.UpdateStatusBanner.Visibility);

            settings.ShowAppRecommendations = false;
            view.Activate();
            Assert.Equal(Visibility.Collapsed, view.SuiteSection.Visibility);

            var current = UpdateService.GetCurrentVersion();
            view.ApplyCheckResult(new UpdateCheckResult(
                current, current, UpdateService.GetCurrentVersionLabel(),
                "https://github.com/CyberGems/CyberWall/releases/tag/v2.4.0",
                null, null, null, null, false, null));
            Assert.Equal(Visibility.Visible, view.UpdateStatusBanner.Visibility);
            var enUpToDate = view.UpdateStatusTitle.Text;

            var newer = new Version(current.Major + 1, 0, 0);
            var checkCount = 0;
            view.CheckForUpdates = () =>
            {
                checkCount++;
                return Task.FromResult(new UpdateCheckResult(
                    current, newer, $"v{newer}",
                    "https://github.com/CyberGems/CyberWall/releases/tag/v99.0.0",
                    "https://example.invalid/setup.exe", "setup.exe", "abc",
                    new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero),
                    true, null));
            };
            view.UpdateBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, checkCount);
            Assert.Equal(Visibility.Visible, view.UpdateStatusBanner.Visibility);
            Assert.Null(view.FindName("UpdateDownloadBtn"));
            Assert.Null(view.FindName("UpdateProgressPanel"));
            Assert.Null(view.FindName("UpdateOpenReleaseBtn"));
            var enAvail = view.UpdateStatusTitle.Text;

            Strings.Current = Lang.Es;
            view.RefreshLanguage();
            Assert.NotEqual(enAvail, view.UpdateStatusTitle.Text);

            view.ApplyCheckResult(UpdateService.CheckFailed("UpdateCheckConnectionError"));
            Assert.Equal(Visibility.Visible, view.UpdateStatusBanner.Visibility);
            Assert.Equal(Strings.T("UpdateCheckConnectionError"), view.UpdateStatusDetail.Text);
            Assert.NotEqual(enUpToDate, view.UpdateStatusTitle.Text);
        });
    }

    [Fact]
    public void UpdateNotice_ShowsLocalizedStagesAndReleaseNotesInEveryTheme()
    {
        PromptStackWindowTests.RunInSta(() =>
        {
            var previousLanguage = Strings.Current;
            var notice = new UpdateNoticeWindow();
            try
            {
                var release = new UpdateCheckResult(new Version(1, 0), new Version(2, 0), "v2.0",
                    "https://github.com/CyberGems/CyberWall/releases/tag/v2.0",
                    "https://github.com/CyberGems/CyberWall/releases/download/v2.0/CyberWall-Setup-v2.0.exe",
                    "CyberWall-Setup-v2.0.exe", new string('a', 64), null, true, null, """
                    ### Highlights
                    - **Faster** searches
                    <details>
                    <summary>Español</summary>
                    - Búsquedas más rápidas
                    </details>
                    """);
                var root = Assert.IsType<Border>(notice.Content);

                foreach (var theme in Enum.GetValues<AppTheme>())
                {
                    ThemeManager.Apply(theme);
                    foreach (var language in new[] { Lang.En, Lang.Es })
                    {
                        Strings.Current = language;
                        notice.Render(release, UpdatePhase.Available, default, false, false, null);
                        Assert.Equal(Strings.T("UpdateViewGitHub"), notice.GitHubButton.Content);
                        Assert.Equal(Strings.T("UpdateSkipVersion"), notice.SkipButton.Content);
                        Assert.Equal(Strings.T("Later"), notice.LaterButton.Content);
                        Assert.Equal(Strings.T("UpdateNow"), notice.PrimaryButton.Content);
                        Assert.True(notice.PrimaryButton.IsEnabled);
                        Assert.Equal(Visibility.Collapsed, notice.DownloadPanel.Visibility);
                        Assert.Equal(language == Lang.En ? "Faster searches" : "Búsquedas más rápidas",
                            Assert.Single(notice.NotesList.Items.Cast<string>()));

                        notice.Render(release, UpdatePhase.Downloading, new(4_000_000, 8_000_000), false, false, null);
                        Assert.Equal(Strings.T("UpdateDownloadBackground"), notice.LaterButton.Content);
                        Assert.Equal(Strings.T("UpdateCancelDownload"), notice.CancelButton.Content);
                        Assert.Equal(Visibility.Visible, notice.DownloadPanel.Visibility);
                        Assert.Equal(Visibility.Collapsed, notice.PrimaryButton.Visibility);
                        Assert.Equal(50, notice.DownloadProgressBar.Value);
                        Assert.Single(notice.NotesList.Items);

                        notice.Render(release, UpdatePhase.Ready, default, false, false, null);
                        Assert.Equal(Strings.T("UpdateRestart"), notice.PrimaryButton.Content);
                        Assert.Equal(Strings.T("Later"), notice.LaterButton.Content);
                        Assert.Equal(Visibility.Collapsed, notice.CancelButton.Visibility);
                        Assert.Single(notice.NotesList.Items);

                        root.Measure(new Size(480, 600));
                        root.Arrange(new Rect(0, 0, 480, root.DesiredSize.Height));
                        root.UpdateLayout();
                        var surface = Assert.IsType<System.Windows.Media.SolidColorBrush>(root.Background);
                        var expected = System.Windows.Media.ColorConverter.ConvertFromString(ThemeManager.Get(theme).CardSecondary);
                        Assert.Equal(expected, surface.Color);
                        Assert.True(notice.PrimaryButton.ActualWidth > 0);
                    }
                }

                notice.Render(release with { AssetSha256 = null, ReleaseNotes = null },
                    UpdatePhase.Available, default, false, false, null);
                Assert.False(notice.PrimaryButton.IsEnabled);
                Assert.Equal(Visibility.Visible, notice.NotesUnavailable.Visibility);
                Assert.Equal(Visibility.Visible, notice.ErrorText.Visibility);
            }
            finally
            {
                notice.Close();
                Strings.Current = previousLanguage;
                ThemeManager.Apply(AppTheme.CyberWall);
            }
        });
    }

    [Fact]
    public void TrafficMonitorView_AdapterModeBadge_ReflectsSettingAndLanguage()
    {
        RunOnSta(() =>
        {
            Strings.Current = Lang.En;
            var settings = new AppSettings();
            var view = new TrafficMonitorView(settings);

            Assert.Equal(Strings.T("NetworkAdapterAutoShort"), view.AdapterModeBadgeText.Text);

            settings.PreferredNetworkAdapterId = "{adapter-guid}";
            view.UpdateAdapterModeBadge();
            var pinnedEn = view.AdapterModeBadgeText.Text;
            Assert.Equal(Strings.T("NetworkAdapterPinnedShort"), pinnedEn);

            Strings.Current = Lang.Es;
            view.RefreshLanguage();
            Assert.Equal(Strings.T("NetworkAdapterPinnedShort"), view.AdapterModeBadgeText.Text);
            Assert.NotEqual(pinnedEn, view.AdapterModeBadgeText.Text);

            settings.PreferredNetworkAdapterId = string.Empty;
            view.UpdateAdapterModeBadge();
            Assert.Equal(Strings.T("NetworkAdapterAutoShort"), view.AdapterModeBadgeText.Text);
        });
    }

    [Fact]
    public void MenuToggleHelper_TogglesAttachedContextMenuCleanly()
    {
        RunOnSta(() =>
        {
            var button = new System.Windows.Controls.Button();
            var menu = new System.Windows.Controls.ContextMenu();
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Item 1" });

            int beforeOpenCalls = 0;
            MenuToggleHelper.Attach(button, menu, beforeOpen: () => beforeOpenCalls++);

            Assert.False(menu.IsOpen);

            MenuToggleHelper.Toggle(button);
            Assert.True(menu.IsOpen);
            Assert.Equal(1, beforeOpenCalls);

            MenuToggleHelper.Toggle(button);
            Assert.False(menu.IsOpen);
            Assert.Equal(1, beforeOpenCalls);

            MenuToggleHelper.Toggle(button);
            Assert.True(menu.IsOpen);
            Assert.Equal(2, beforeOpenCalls);

            menu.IsOpen = false;
        });
    }

    [Fact]
    public void MenuToggleHelper_ArmedExternalDismiss_PreventsImmediateReopen()
    {
        RunOnSta(() =>
        {
            var button = new System.Windows.Controls.Button();
            var menu = new System.Windows.Controls.ContextMenu();
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Item 1" });

            MenuToggleHelper.Attach(button, menu);

            Assert.True(MenuToggleHelper.Toggle(button));
            Assert.True(menu.IsOpen);

            // Simulate WPF MenuBase auto-dismissing the ContextMenu on MouseDown over trigger button
            menu.IsOpen = false;
            MenuToggleHelper.ArmExternalDismissForTest(button);

            // Subsequent MouseUp -> Button.Click must consume suppression and stay closed
            Assert.False(MenuToggleHelper.Toggle(button));
            Assert.False(menu.IsOpen);

            // Next click must reopen cleanly
            Assert.True(MenuToggleHelper.Toggle(button));
            Assert.True(menu.IsOpen);

            menu.IsOpen = false;
        });
    }

    [Fact]
    public void NetworkAdapterMenuHelper_PopulatesAutoAndPreservesSavedSelection()
    {
        RunOnSta(() =>
        {
            Strings.Current = Lang.En;
            var host = new System.Windows.Controls.Grid();
            var menu = new System.Windows.Controls.ContextMenu();
            var settings = new AppSettings
            {
                PreferredNetworkAdapterId = "{missing-test-adapter}",
                PreferredNetworkAdapterName = "Offline Adapter"
            };

            int callbacks = 0;
            NetworkAdapterMenuHelper.Populate(
                host,
                menu.Items,
                settings,
                onSelected: () => callbacks++,
                persistChanges: false);

            var autoItem = Assert.IsType<System.Windows.Controls.MenuItem>(menu.Items[0]);
            Assert.Equal(Strings.T("NetworkAdapterAuto"), autoItem.Header);
            Assert.False(autoItem.IsChecked);

            var savedFallback = menu.Items.OfType<System.Windows.Controls.MenuItem>().Last();
            Assert.True(savedFallback.IsChecked);

            autoItem.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            Assert.Equal(1, callbacks);
            Assert.Equal(string.Empty, settings.PreferredNetworkAdapterId);
        });
    }
}


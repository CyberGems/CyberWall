using CyberWall.Common.Models;
using CyberWall.Common.Notifications;

namespace CyberWall.Tests;

public class NotificationStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"cyberwall-notifications-{Guid.NewGuid():N}.json");

    [Fact]
    public void APendingDecisionIsReusedAfterBeingRead()
    {
        var path = TempPath();
        try
        {
            var store = new NotificationStore(path);
            store.Add(AppNotificationKind.PendingDecision, @"C:\Apps\Demo.exe", "Demo", "1.2.3.4:443");
            store.MarkAllRead();

            store.Add(AppNotificationKind.PendingDecision, @"C:\Apps\Demo.exe", "Demo", "5.6.7.8:443");

            var item = Assert.Single(store.All);
            Assert.False(item.Read);
            Assert.Equal("5.6.7.8:443", item.Detail);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void RemoveRelatedOnlyRemovesTheRequestedKindAndApp()
    {
        var path = TempPath();
        try
        {
            var store = new NotificationStore(path);
            store.Add(AppNotificationKind.PendingDecision, @"C:\Apps\A.exe", "A");
            store.Add(AppNotificationKind.PendingDecision, @"C:\Apps\B.exe", "B");
            store.Add(AppNotificationKind.AutoBlocked, @"C:\Apps\A.exe", "A");

            store.RemoveRelated(AppNotificationKind.PendingDecision, @"C:\Apps\A.exe");

            Assert.DoesNotContain(store.All, n =>
                n.Kind == AppNotificationKind.PendingDecision &&
                n.AppPath == @"C:\Apps\A.exe");
            Assert.Contains(store.All, n =>
                n.Kind == AppNotificationKind.PendingDecision &&
                n.AppPath == @"C:\Apps\B.exe");
            Assert.Contains(store.All, n =>
                n.Kind == AppNotificationKind.AutoBlocked &&
                n.AppPath == @"C:\Apps\A.exe");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

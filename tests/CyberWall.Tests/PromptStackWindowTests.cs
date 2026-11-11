using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Popup;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class PromptStackWindowTests
{
    private static readonly object StaLock = new();
    private static Dispatcher? _staDispatcher;

    private static void RunInSta(Action action)
    {
        var dispatcher = GetOrStartStaDispatcher();
        Exception? captured = null;
        dispatcher.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured != null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static Dispatcher GetOrStartStaDispatcher()
    {
        lock (StaLock)
        {
            if (_staDispatcher != null)
            {
                return _staDispatcher;
            }

            using var ready = new ManualResetEventSlim(false);
            Dispatcher? created = null;
            var thread = new Thread(() =>
            {
                if (System.Windows.Application.Current == null)
                {
                    _ = new System.Windows.Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }
                ThemeManager.Apply(AppTheme.CyberWall);

                created = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            ready.Wait();
            _staDispatcher = created!;
            return _staDispatcher;
        }
    }

    private static ConnectionEvent MakeEvent(string appPath) => new()
    {
        AppPath = appPath,
        RemoteAddress = "93.184.216.34",
        RemotePort = 443,
        Protocol = "TCP",
        Direction = Direction.Outbound,
        ProcessId = 4242
    };

    [Fact]
    public void ExternalCloseResolvesAllPendingCardsAsDismissOnce()
    {
        RunInSta(() =>
        {
            var resolved = new List<(string AppPath, PopupDecision Decision, bool TimedOut)>();
            var win = new PromptStackWindow
            {
                Topmost = false,
                Opacity = 0
            };
            win.CardResolved += (card, decision, timedOut) =>
                resolved.Add((card.Event.AppPath, decision, timedOut));

            win.AddCard(MakeEvent(@"C:\Tests\AppOne.exe"));
            win.AddCard(MakeEvent(@"C:\Tests\AppTwo.exe"));

            win.Show();
            win.Close();

            Assert.Equal(0, win.ActiveCount);
            Assert.Equal(2, resolved.Count);
            Assert.All(resolved, item =>
            {
                Assert.Equal(PopupDecision.Dismiss, item.Decision);
                Assert.False(item.TimedOut);
            });
            Assert.Equal(@"C:\Tests\AppOne.exe", resolved[0].AppPath);
            Assert.Equal(@"C:\Tests\AppTwo.exe", resolved[1].AppPath);
        });
    }

    [Fact]
    public void ExplicitDismissAndIndividualDecisionsDoNotDoubleResolveOnClose()
    {
        RunInSta(() =>
        {
            var dismissResolved = new List<(string AppPath, PopupDecision Decision, bool TimedOut)>();
            var win1 = new PromptStackWindow
            {
                Topmost = false,
                Opacity = 0
            };
            win1.CardResolved += (card, decision, timedOut) =>
                dismissResolved.Add((card.Event.AppPath, decision, timedOut));

            win1.AddCard(MakeEvent(@"C:\Tests\BulkOne.exe"));
            win1.AddCard(MakeEvent(@"C:\Tests\BulkTwo.exe"));
            win1.Show();
            win1.DismissAll();

            Assert.Equal(2, dismissResolved.Count);
            Assert.All(dismissResolved, item => Assert.Equal(PopupDecision.Dismiss, item.Decision));

            var singleResolved = new List<(string AppPath, PopupDecision Decision, bool TimedOut)>();
            var win2 = new PromptStackWindow
            {
                Topmost = false,
                Opacity = 0
            };
            win2.CardResolved += (card, decision, timedOut) =>
                singleResolved.Add((card.Event.AppPath, decision, timedOut));

            var ev1 = MakeEvent(@"C:\Tests\SingleOne.exe");
            var ev2 = MakeEvent(@"C:\Tests\SingleTwo.exe");
            win2.AddCard(ev1);
            win2.AddCard(ev2);
            win2.Show();

            var card1 = win2.CardsContainer.Children.OfType<PromptCardControl>().First();
            card1.TriggerDecision(PopupDecision.AllowOnce);
            var card2 = win2.CardsContainer.Children.OfType<PromptCardControl>().First();
            card2.TriggerDecision(PopupDecision.AllowAlways);

            Assert.Equal(0, win2.ActiveCount);
            Assert.Equal(2, singleResolved.Count);
            Assert.Equal(PopupDecision.AllowOnce, singleResolved[0].Decision);
            Assert.Equal(PopupDecision.AllowAlways, singleResolved[1].Decision);
        });
    }
}

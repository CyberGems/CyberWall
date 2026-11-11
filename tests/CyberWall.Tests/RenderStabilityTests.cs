using System.Reflection;
using System.Runtime.InteropServices;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class RenderStabilityTests
{
    [Fact]
    public void DetectsDirectAndWrappedRenderThreadFailureComExceptions()
    {
        var direct = new COMException("UCEERR_RENDERTHREADFAILURE", RenderPauseState.RenderThreadFailureHResult);
        var wrapped = new TargetInvocationException(new InvalidOperationException("outer", direct));
        var aggregated = new AggregateException(new InvalidOperationException("unrelated"), direct);
        var otherCom = new COMException("E_FAIL", unchecked((int)0x80004005));

        Assert.True(RenderPauseState.IsRenderThreadFailure(direct));
        Assert.True(RenderPauseState.IsRenderThreadFailure(wrapped));
        Assert.True(RenderPauseState.IsRenderThreadFailure(aggregated));
        Assert.False(RenderPauseState.IsRenderThreadFailure(otherCom));
        Assert.False(RenderPauseState.IsRenderThreadFailure(new InvalidOperationException("normal")));
        Assert.False(RenderPauseState.IsRenderThreadFailure(null));
    }

    [Fact]
    public void ClassifiesPowerAndDisplayMessagesWithoutFalsePositivesOnBatteryChanges()
    {
        const int wmPowerBroadcast = 0x0218;
        const int wmDisplayChange = 0x007E;
        const int wmDwmCompositionChanged = 0x031E;
        const int wmDpiChanged = 0x02E0;

        Assert.Equal(PowerBroadcastAction.Suspend, RenderPauseState.ClassifyWindowMessage(wmPowerBroadcast, new IntPtr(0x0004)));
        Assert.Equal(PowerBroadcastAction.Resume, RenderPauseState.ClassifyWindowMessage(wmPowerBroadcast, new IntPtr(0x0006)));
        Assert.Equal(PowerBroadcastAction.Resume, RenderPauseState.ClassifyWindowMessage(wmPowerBroadcast, new IntPtr(0x0007)));
        Assert.Equal(PowerBroadcastAction.Resume, RenderPauseState.ClassifyWindowMessage(wmPowerBroadcast, new IntPtr(0x0012)));
        Assert.Equal(PowerBroadcastAction.None, RenderPauseState.ClassifyWindowMessage(wmPowerBroadcast, new IntPtr(0x000A)));
        Assert.Equal(PowerBroadcastAction.DisplayTransition, RenderPauseState.ClassifyWindowMessage(wmDisplayChange, IntPtr.Zero));
        Assert.Equal(PowerBroadcastAction.DisplayTransition, RenderPauseState.ClassifyWindowMessage(wmDwmCompositionChanged, IntPtr.Zero));
        Assert.Equal(PowerBroadcastAction.None, RenderPauseState.ClassifyWindowMessage(wmDpiChanged, IntPtr.Zero));
    }

    [Fact]
    public void HoldsPauseAcrossResumeSettleAndRefusesToShortenLongerCooldown()
    {
        var state = new RenderPauseState();
        var t0 = new DateTime(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);

        var suspend = state.BeginSuspend();
        Assert.True(suspend.Changed);
        Assert.True(suspend.IsPaused);

        var resume = state.BeginResume(t0, TimeSpan.FromMilliseconds(1800));
        Assert.False(resume.Changed);
        Assert.True(resume.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(1800), resume.TimerDelay);

        // A shorter display-change settle 200ms after wake must not pull the 1800ms resume deadline earlier.
        var display = state.TriggerCooldown(t0.AddMilliseconds(200), TimeSpan.FromMilliseconds(600));
        Assert.False(display.Changed);
        Assert.True(display.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(1600), display.TimerDelay);

        var premature = state.TryFinishCooldown(t0.AddMilliseconds(1000));
        Assert.False(premature.Changed);
        Assert.True(premature.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(800), premature.RemainingDelay);

        var finished = state.TryFinishCooldown(t0.AddMilliseconds(1800));
        Assert.True(finished.Changed);
        Assert.False(finished.IsPaused);
        Assert.Null(finished.RemainingDelay);
    }

    [Fact]
    public void KeepsRenderingPausedUntilBothSuspendAndSessionLockClear()
    {
        var state = new RenderPauseState();
        var t0 = new DateTime(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);

        state.BeginSessionLock();
        state.BeginSuspend();

        var resumedLocked = state.BeginResume(t0, TimeSpan.FromMilliseconds(1800));
        Assert.True(resumedLocked.IsPaused);
        Assert.Null(resumedLocked.TimerDelay);

        var stillLocked = state.TryFinishCooldown(t0.AddSeconds(10));
        Assert.False(stillLocked.Changed);
        Assert.True(stillLocked.IsPaused);

        var unlocked = state.EndSessionLock(t0.AddSeconds(11), TimeSpan.FromMilliseconds(800));
        Assert.True(unlocked.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(800), unlocked.TimerDelay);

        var afterUnlock = state.TryFinishCooldown(t0.AddSeconds(12));
        Assert.True(afterUnlock.Changed);
        Assert.False(afterUnlock.IsPaused);
    }

    [Fact]
    public void CapsRepeatedSoftwareRenderFailuresWithinWindow()
    {
        var state = new RenderPauseState();
        var t0 = new DateTime(2026, 11, 11, 15, 0, 0, DateTimeKind.Utc);

        // First transition to software mode is always handled.
        Assert.True(state.RecordRenderFailureAndShouldHandle(alreadySoftwareOnly: false, t0));

        // Up to MaxSoftwareFailuresPerWindow transient frames are drained cleanly.
        for (int i = 0; i < RenderPauseState.MaxSoftwareFailuresPerWindow; i++)
        {
            Assert.True(state.RecordRenderFailureAndShouldHandle(alreadySoftwareOnly: true, t0.AddMilliseconds(i * 100)));
        }

        // Excess tight-loop failures in the same window are not swallowed.
        Assert.False(state.RecordRenderFailureAndShouldHandle(alreadySoftwareOnly: true, t0.AddMilliseconds(600)));

        // Old failures expire once outside the sliding window.
        Assert.True(state.RecordRenderFailureAndShouldHandle(
            alreadySoftwareOnly: true,
            t0.Add(RenderPauseState.FailureWindow).AddMilliseconds(50)));
    }
}


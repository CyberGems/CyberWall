using System.Collections;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CyberWall.UI.Services;

internal enum PowerBroadcastAction
{
    None,
    Suspend,
    Resume,
    DisplayTransition
}

/// <summary>
/// Tracks power/session/display transitions and software-render crash limits independently of WPF handles,
/// keeping the state transitions deterministic and unit-testable.
/// </summary>
internal sealed class RenderPauseState
{
    public const int RenderThreadFailureHResult = unchecked((int)0x88980406);
    public const int MaxSoftwareFailuresPerWindow = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(5);

    private readonly object _sync = new();
    private readonly Queue<DateTime> _softwareFailureTimes = new();
    private bool _inSuspend;
    private bool _sessionLocked;
    private DateTime? _cooldownUntilUtc;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            }
        }
    }

    public static PowerBroadcastAction ClassifyWindowMessage(int msg, IntPtr wParam)
    {
        const int WM_DISPLAYCHANGE = 0x007E;
        const int WM_POWERBROADCAST = 0x0218;
        const int WM_DWMCOMPOSITIONCHANGED = 0x031E;

        const int PBT_APMSUSPEND = 0x0004;
        const int PBT_APMRESUMECRITICAL = 0x0006;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;

        if (msg == WM_POWERBROADCAST)
        {
            return (int)wParam.ToInt64() switch
            {
                PBT_APMSUSPEND => PowerBroadcastAction.Suspend,
                PBT_APMRESUMECRITICAL or PBT_APMRESUMESUSPEND or PBT_APMRESUMEAUTOMATIC => PowerBroadcastAction.Resume,
                _ => PowerBroadcastAction.None
            };
        }

        if (msg is WM_DISPLAYCHANGE or WM_DWMCOMPOSITIONCHANGED)
        {
            return PowerBroadcastAction.DisplayTransition;
        }

        return PowerBroadcastAction.None;
    }

    public static bool IsRenderThreadFailure(Exception? exception)
    {
        if (exception is null) return false;

        var queue = new Queue<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        queue.Enqueue(exception);
        visited.Add(exception);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is COMException com && com.HResult == RenderThreadFailureHResult)
            {
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (visited.Add(inner))
                    {
                        queue.Enqueue(inner);
                    }
                }
            }

            if (current.InnerException is not null && visited.Add(current.InnerException))
            {
                queue.Enqueue(current.InnerException);
            }
        }

        return false;
    }

    public (bool Changed, bool IsPaused) BeginSuspend()
    {
        lock (_sync)
        {
            bool wasPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            _inSuspend = true;
            _cooldownUntilUtc = null;
            return (!wasPaused, true);
        }
    }

    public (bool Changed, bool IsPaused, TimeSpan? TimerDelay) BeginResume(DateTime utcNow, TimeSpan settleDelay)
    {
        lock (_sync)
        {
            bool wasPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            _inSuspend = false;
            var delay = ExtendCooldownLocked(utcNow, settleDelay);
            bool isPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            return (wasPaused != isPaused, isPaused, delay);
        }
    }

    public (bool Changed, bool IsPaused) BeginSessionLock()
    {
        lock (_sync)
        {
            bool wasPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            _sessionLocked = true;
            return (!wasPaused, true);
        }
    }

    public (bool Changed, bool IsPaused, TimeSpan? TimerDelay) EndSessionLock(DateTime utcNow, TimeSpan settleDelay)
    {
        lock (_sync)
        {
            bool wasPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            _sessionLocked = false;
            var delay = ExtendCooldownLocked(utcNow, settleDelay);
            bool isPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            return (wasPaused != isPaused, isPaused, delay);
        }
    }

    public (bool Changed, bool IsPaused, TimeSpan? TimerDelay) TriggerCooldown(DateTime utcNow, TimeSpan delay)
    {
        lock (_sync)
        {
            bool wasPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            var timerDelay = ExtendCooldownLocked(utcNow, delay);
            bool isPaused = _inSuspend || _sessionLocked || _cooldownUntilUtc.HasValue;
            return (wasPaused != isPaused, isPaused, timerDelay);
        }
    }

    public (bool Changed, bool IsPaused, TimeSpan? RemainingDelay) TryFinishCooldown(DateTime utcNow)
    {
        lock (_sync)
        {
            if (_inSuspend || _sessionLocked)
            {
                _cooldownUntilUtc = null;
                return (false, true, null);
            }

            if (_cooldownUntilUtc is not DateTime deadline)
            {
                return (false, false, null);
            }

            if (utcNow < deadline)
            {
                var remaining = deadline - utcNow;
                if (remaining < TimeSpan.FromMilliseconds(50))
                {
                    remaining = TimeSpan.FromMilliseconds(50);
                }
                return (false, true, remaining);
            }

            _cooldownUntilUtc = null;
            return (true, false, null);
        }
    }

    public bool RecordRenderFailureAndShouldHandle(bool alreadySoftwareOnly, DateTime utcNow)
    {
        if (!alreadySoftwareOnly)
        {
            return true;
        }

        lock (_sync)
        {
            var cutoff = utcNow - FailureWindow;
            while (_softwareFailureTimes.Count > 0 && _softwareFailureTimes.Peek() < cutoff)
            {
                _softwareFailureTimes.Dequeue();
            }

            if (_softwareFailureTimes.Count >= MaxSoftwareFailuresPerWindow)
            {
                return false;
            }

            _softwareFailureTimes.Enqueue(utcNow);
            return true;
        }
    }

    private TimeSpan? ExtendCooldownLocked(DateTime utcNow, TimeSpan delay)
    {
        var candidate = utcNow + delay;
        if (_cooldownUntilUtc is null || candidate > _cooldownUntilUtc.Value)
        {
            _cooldownUntilUtc = candidate;
        }

        if (_inSuspend || _sessionLocked)
        {
            return null;
        }

        var remaining = _cooldownUntilUtc.Value - utcNow;
        return remaining > TimeSpan.FromMilliseconds(50)
            ? remaining
            : TimeSpan.FromMilliseconds(50);
    }
}

/// <summary>
/// Coordinates graceful pause/resume of continuous WPF animations around sleep, lock, and display transitions,
/// and recovers cleanly to software rendering if <c>UCEERR_RENDERTHREADFAILURE (0x88980406)</c> occurs.
/// </summary>
internal sealed class RenderStabilityManager
{
    private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static readonly TimeSpan ResumeSettleDelay = TimeSpan.FromMilliseconds(1800);
    private static readonly TimeSpan SessionUnlockSettleDelay = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan DisplayChangeSettleDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan RecoverySettleDelay = TimeSpan.FromMilliseconds(1000);

    private static readonly Lazy<RenderStabilityManager> _instance = new(() => new RenderStabilityManager());
    public static RenderStabilityManager Instance => _instance.Value;

    private readonly RenderPauseState _state = new();
    private readonly Dictionary<IntPtr, IntPtr> _powerNotifyHandles = new();
    private System.Windows.Application? _app;
    private Dispatcher? _dispatcher;
    private DispatcherTimer? _resumeTimer;
    private int _isRenderPausedFlag;
    private int _isSoftwareForcedFlag;
    private bool _lastPublishedPaused;
    private bool _attached;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterSuspendResumeNotification(IntPtr hRecipient, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterSuspendResumeNotification(IntPtr handle);

    public bool IsRenderPaused => Volatile.Read(ref _isRenderPausedFlag) != 0;
    public bool IsSoftwareRenderForced => Volatile.Read(ref _isSoftwareForcedFlag) != 0;

    public event Action<bool>? RenderPausedChanged;
    public event Action? SoftwareFallbackActivated;

    private RenderStabilityManager()
    {
    }

    public void Attach(System.Windows.Application app)
    {
        if (_attached) return;
        _attached = true;
        _app = app;
        _dispatcher = app.Dispatcher;

        _resumeTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = ResumeSettleDelay
        };
        _resumeTimer.Tick += OnResumeTimerTick;

        app.DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch
        {
            // Restricted environments may block SystemEvents hidden window creation; WndProc hooks still provide coverage.
        }
    }

    public void Detach()
    {
        if (!_attached) return;
        _attached = false;

        _resumeTimer?.Stop();

        if (_app != null)
        {
            _app.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            _app = null;
        }

        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
        catch { }

        foreach (var notifyHandle in _powerNotifyHandles.Values)
        {
            try
            {
                if (notifyHandle != IntPtr.Zero)
                    UnregisterSuspendResumeNotification(notifyHandle);
            }
            catch { }
        }
        _powerNotifyHandles.Clear();
    }

    /// <summary>
    /// Registers a top-level HWND so Windows delivers Modern Standby (<c>WM_POWERBROADCAST</c>) notifications directly.
    /// </summary>
    public void RegisterWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _powerNotifyHandles.ContainsKey(hwnd))
            return;

        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
        {
            _powerNotifyHandles[hwnd] = IntPtr.Zero;
            return;
        }

        try
        {
            var notifyHandle = RegisterSuspendResumeNotification(hwnd, DEVICE_NOTIFY_WINDOW_HANDLE);
            _powerNotifyHandles[hwnd] = notifyHandle;
        }
        catch
        {
            _powerNotifyHandles[hwnd] = IntPtr.Zero;
        }
    }

    public void UnregisterWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (_powerNotifyHandles.Remove(hwnd, out var notifyHandle) && notifyHandle != IntPtr.Zero)
        {
            try
            {
                UnregisterSuspendResumeNotification(notifyHandle);
            }
            catch { }
        }
    }

    public void HandleWindowMessage(int msg, IntPtr wParam)
    {
        switch (RenderPauseState.ClassifyWindowMessage(msg, wParam))
        {
            case PowerBroadcastAction.Suspend:
                ApplySuspend();
                break;
            case PowerBroadcastAction.Resume:
                ApplyResume(ResumeSettleDelay);
                break;
            case PowerBroadcastAction.DisplayTransition:
                ApplyCooldown(DisplayChangeSettleDelay);
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            ApplySuspend();
        }
        else if (e.Mode == PowerModes.Resume)
        {
            ApplyResume(ResumeSettleDelay);
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                var locked = _state.BeginSessionLock();
                PublishPauseState(locked.IsPaused, stopTimer: true, nextDelay: null);
                break;

            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                var unlocked = _state.EndSessionLock(DateTime.UtcNow, SessionUnlockSettleDelay);
                PublishPauseState(unlocked.IsPaused, stopTimer: false, nextDelay: unlocked.TimerDelay);
                break;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        ApplyCooldown(DisplayChangeSettleDelay);
    }

    private void ApplySuspend()
    {
        var result = _state.BeginSuspend();
        PublishPauseState(result.IsPaused, stopTimer: true, nextDelay: null);
    }

    private void ApplyResume(TimeSpan settleDelay)
    {
        var result = _state.BeginResume(DateTime.UtcNow, settleDelay);
        PublishPauseState(result.IsPaused, stopTimer: false, nextDelay: result.TimerDelay);
    }

    private void ApplyCooldown(TimeSpan delay)
    {
        var result = _state.TriggerCooldown(DateTime.UtcNow, delay);
        PublishPauseState(result.IsPaused, stopTimer: false, nextDelay: result.TimerDelay);
    }

    private void PublishPauseState(bool isPaused, bool stopTimer, TimeSpan? nextDelay)
    {
        Volatile.Write(ref _isRenderPausedFlag, isPaused ? 1 : 0);

        var dispatcher = _dispatcher;
        if (dispatcher == null) return;

        void SyncOnDispatcher()
        {
            if (stopTimer)
            {
                _resumeTimer?.Stop();
            }
            else if (nextDelay.HasValue && _resumeTimer != null && _state.IsPaused)
            {
                _resumeTimer.Stop();
                _resumeTimer.Interval = nextDelay.Value;
                _resumeTimer.Start();
            }

            PublishPauseEventIfChanged();
        }

        if (dispatcher.CheckAccess())
        {
            SyncOnDispatcher();
        }
        else
        {
            dispatcher.BeginInvoke(SyncOnDispatcher);
        }
    }

    private void OnResumeTimerTick(object? sender, EventArgs e)
    {
        _resumeTimer?.Stop();

        var result = _state.TryFinishCooldown(DateTime.UtcNow);
        Volatile.Write(ref _isRenderPausedFlag, result.IsPaused ? 1 : 0);

        if (result.RemainingDelay.HasValue && _resumeTimer != null)
        {
            _resumeTimer.Interval = result.RemainingDelay.Value;
            _resumeTimer.Start();
            return;
        }

        PublishPauseEventIfChanged();
    }

    private void PublishPauseEventIfChanged()
    {
        bool current = IsRenderPaused;
        if (current == _lastPublishedPaused) return;
        _lastPublishedPaused = current;
        RenderPausedChanged?.Invoke(current);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!RenderPauseState.IsRenderThreadFailure(e.Exception))
        {
            return;
        }

        bool alreadySoftwareOnly = IsSoftwareRenderForced || RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly;
        if (!_state.RecordRenderFailureAndShouldHandle(alreadySoftwareOnly, DateTime.UtcNow))
        {
            return;
        }

        ForceSoftwareRendering();
        ApplyCooldown(RecoverySettleDelay);
        e.Handled = true;
    }

    private void ForceSoftwareRendering()
    {
        bool firstActivation = Interlocked.Exchange(ref _isSoftwareForcedFlag, 1) == 0;

        try
        {
            if (RenderOptions.ProcessRenderMode != RenderMode.SoftwareOnly)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            // Also flip existing live HwndTargets (main window, floating widget, popups) immediately.
            var targets = new List<HwndTarget>();
            foreach (var obj in PresentationSource.CurrentSources)
            {
                if (obj is HwndSource { IsDisposed: false, CompositionTarget: { } target })
                {
                    targets.Add(target);
                }
            }

            foreach (var target in targets)
            {
                try
                {
                    target.RenderMode = RenderMode.SoftwareOnly;
                }
                catch { }
            }
        }
        catch { }

        if (firstActivation)
        {
            try
            {
                SoftwareFallbackActivated?.Invoke();
            }
            catch { }
        }
    }
}

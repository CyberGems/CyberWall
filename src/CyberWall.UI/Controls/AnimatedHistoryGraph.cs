using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CyberWall.UI.Controls;

/// <summary>
/// Moves retained graph drawings smoothly between real metric samples using hardware TranslateTransform,
/// with prefix retention to avoid border gaps, and zero per-frame allocations.
/// </summary>
public abstract class AnimatedHistoryGraph : FrameworkElement
{
    public static readonly DependencyProperty IsAnimationEnabledProperty = DependencyProperty.Register(
        nameof(IsAnimationEnabled), typeof(bool), typeof(AnimatedHistoryGraph),
        new PropertyMetadata(true, OnAnimationEnabledChanged));

    private readonly TranslateTransform _historyTransform = new();
    private object? _lastSample;
    private float[]? _previousPrimary;
    private float[]? _previousSecondary;
    private Window? _window;
    private long _startedAt;
    private double _durationSeconds;
    private double _startOffsetSamples;
    private double _stepWidth;
    private bool _animating;
    private float[] _primaryPrefix = Array.Empty<float>();
    private float[] _secondaryPrefix = Array.Empty<float>();

    protected AnimatedHistoryGraph()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => { if (!CanAnimate) FinishAnimation(); };
    }

    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    protected TranslateTransform HistoryTransform => _historyTransform;
    protected IReadOnlyList<float> PrimaryPrefix => _primaryPrefix;
    protected IReadOnlyList<float> SecondaryPrefix => _secondaryPrefix;
    protected double HistoryOffset => _historyTransform.X;
    internal bool IsAnimating => _animating;
    protected bool IsCurrentSample(object sample) => ReferenceEquals(_lastSample, sample);

    private bool CanAnimate => IsAnimationEnabled && SystemParameters.ClientAreaAnimation && IsLoaded && IsVisible &&
                               _window is { IsVisible: true, WindowState: not WindowState.Minimized };

    protected void BeginSample(object sample, TimeSpan interval, float[]? primary, float[]? secondary, int capacity)
    {
        if (ReferenceEquals(_lastSample, sample)) return;
        var consecutive = _lastSample is not null && IsNextHistory(_previousPrimary, primary, capacity);
        var startOffset = 1 + (_animating && _stepWidth > 0 ? _historyTransform.X / _stepWidth : 0);
        var prefixCount = Math.Max(1, (int)Math.Ceiling(startOffset));

        // Keep outgoing points still inside the clip if a sample arrives before the previous slide finishes
        _primaryPrefix = RetainPrefix(_primaryPrefix, _previousPrimary, primary, capacity, consecutive, prefixCount);
        _secondaryPrefix = RetainPrefix(_secondaryPrefix, _previousSecondary, secondary, capacity,
            consecutive && IsNextHistory(_previousSecondary, secondary, capacity), prefixCount);

        _lastSample = sample;
        _previousPrimary = primary;
        _previousSecondary = secondary;

        if (consecutive && CanAnimate && interval > TimeSpan.Zero)
        {
            _startOffsetSamples = startOffset;
            _durationSeconds = interval.TotalSeconds;
            _startedAt = Stopwatch.GetTimestamp();
            _animating = true;
            _historyTransform.X = _startOffsetSamples * _stepWidth;
            OnAnimationStarted();
            GraphAnimationClock.For(Dispatcher).Add(this);
        }
        else
        {
            FinishAnimation();
        }

        InvalidateVisual();
    }

    protected void SetHistoryWidth(double width, int capacity)
    {
        var oldWidth = _stepWidth;
        _stepWidth = width / Math.Max(1, capacity - 1);
        if (_animating)
        {
            _historyTransform.X = oldWidth > 0
                ? _historyTransform.X * _stepWidth / oldWidth
                : _startOffsetSamples * _stepWidth;
        }
    }

    protected virtual void OnAnimationStarted() { }
    protected virtual void OnAnimationFrame(double elapsedSeconds, bool finished) { }

    internal bool AdvanceAnimation(long now)
    {
        if (!_animating) return false;
        if (!CanAnimate) { FinishAnimation(); return false; }

        var elapsed = Stopwatch.GetElapsedTime(_startedAt, now).TotalSeconds;
        var progress = Math.Clamp(elapsed / _durationSeconds, 0, 1);
        _historyTransform.X = _startOffsetSamples * (1 - progress) * _stepWidth;
        OnAnimationFrame(elapsed, progress >= 1);
        if (progress >= 1) FinishAnimation();

        InvalidateVisual();
        return _animating;
    }

    private void FinishAnimation()
    {
        _animating = false;
        _historyTransform.X = 0;
        OnAnimationFrame(0, finished: true);
        GraphAnimationClock.For(Dispatcher).Remove(this);
        InvalidateVisual();
    }

    public void ResetAnimation()
    {
        _lastSample = null;
        _previousPrimary = null;
        _previousSecondary = null;
        _primaryPrefix = Array.Empty<float>();
        _secondaryPrefix = Array.Empty<float>();
        _historyTransform.X = 0;
        FinishAnimation();
    }

    public void SetCurrentState(object sample, float[]? primary, float[]? secondary, int capacity)
    {
        _lastSample = sample;
        _previousPrimary = primary;
        _previousSecondary = secondary;
        _primaryPrefix = Array.Empty<float>();
        _secondaryPrefix = Array.Empty<float>();
        _animating = false;
        _historyTransform.X = 0;
        SetHistoryWidth(ActualWidth, capacity);
        GraphAnimationClock.For(Dispatcher).Remove(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _window = Window.GetWindow(this);
        if (_window is not null) _window.StateChanged += OnWindowStateChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_window is not null) _window.StateChanged -= OnWindowStateChanged;
        _window = null;
        FinishAnimation();
    }

    private void OnWindowStateChanged(object? sender, EventArgs args)
    {
        if (!CanAnimate) FinishAnimation();
    }

    private static void OnAnimationEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (!(bool)args.NewValue) ((AnimatedHistoryGraph)element).FinishAnimation();
    }

    private static bool IsNextHistory(float[]? previous, float[]? current, int capacity)
    {
        if (previous is not { Length: > 0 } || current is null ||
            current.Length != Math.Min(previous.Length + 1, capacity)) return false;

        var skipped = previous.Length == capacity ? 1 : 0;
        for (var index = 0; index < current.Length - 1; index++)
        {
            if (Math.Abs(current[index] - previous[index + skipped]) > 0.0001f) return false;
        }
        return true;
    }

    private static float[] RetainPrefix(float[] previousPrefix, float[]? previous, float[]? current,
        int capacity, bool consecutive, int needed)
    {
        if (!consecutive || previous is null || current is null || current.Length < capacity) return Array.Empty<float>();
        var result = new float[Math.Min(needed, previousPrefix.Length + 1)];
        for (var index = 0; index < result.Length - 1; index++)
            result[index] = previousPrefix[previousPrefix.Length - result.Length + 1 + index];
        result[^1] = previous[0];
        return result;
    }

    private sealed class GraphAnimationClock
    {
        private static readonly ConditionalWeakTable<Dispatcher, GraphAnimationClock> Clocks = new();
        private readonly List<AnimatedHistoryGraph> _graphs = new();
        private readonly DispatcherTimer _timer;

        private GraphAnimationClock(Dispatcher dispatcher)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1.0 / 30)
            };
            _timer.Tick += (_, _) =>
            {
                var now = Stopwatch.GetTimestamp();
                for (var index = _graphs.Count - 1; index >= 0; index--)
                {
                    _graphs[index].AdvanceAnimation(now);
                }
            };
        }

        internal static GraphAnimationClock For(Dispatcher dispatcher) =>
            Clocks.GetValue(dispatcher, key => new GraphAnimationClock(key));

        internal void Add(AnimatedHistoryGraph graph)
        {
            if (!_graphs.Contains(graph)) _graphs.Add(graph);
            _timer.Start();
        }

        internal void Remove(AnimatedHistoryGraph graph)
        {
            _graphs.Remove(graph);
            if (_graphs.Count == 0) _timer.Stop();
        }
    }
}


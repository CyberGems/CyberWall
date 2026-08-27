using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI.Popup;

public partial class PromptStackWindow : Window
{
    private const int DefaultTimeoutSeconds = 300;
    private const int MaxVisibleCards = 2;

    private static PromptStackWindow? _activePreview;
    private static DispatcherTimer? _previewTimer;

    private readonly List<PromptCardControl> _cards = new();
    private readonly DispatcherTimer _countdownTimer;
    private readonly bool _autoBlockEnabled;
    private int _remaining = DefaultTimeoutSeconds;
    private bool _countdownPaused;

    public bool IsPreview { get; }
    public int ActiveCount => _cards.Count;
    public event Action<PromptCardControl, PopupDecision, bool>? CardResolved;

    public PromptStackWindow(bool isPreview = false)
    {
        InitializeComponent();
        IsPreview = isPreview;
        DataContext = this;

        var timeoutSeconds = Math.Clamp(App.Settings.PopupAutoBlockSeconds, 15, 3600);
        _autoBlockEnabled = !isPreview && App.Settings.PopupAutoBlockEnabled;
        _remaining = timeoutSeconds;

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        MouseEnter += (_, _) => _countdownPaused = true;
        MouseLeave += (_, _) => _countdownPaused = false;
        TopBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); } catch { }
        };

        ApplyCopy();

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => TickCountdown();

        if (IsPreview)
        {
            SettingsBtn.Visibility = Visibility.Collapsed;
            CountdownBadge.Visibility = Visibility.Collapsed;
        }
        else if (!_autoBlockEnabled)
        {
            CountdownBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            UpdateCountdownLabel();
        }
    }

    private void ApplyCopy()
    {
        HeaderTitleLbl.Text = Strings.T("PromptStackTitle");
        AllowAllBtn.Content = Strings.T("AllowAllAlways");
        BlockAllBtn.Content = Strings.T("BlockAllPrompts");
        SettingsBtn.ToolTip = Strings.T("Settings");
        DismissAllBtn.ToolTip = Strings.T("DismissAllTooltip");
        AutomationProperties.SetName(SettingsBtn, Strings.T("Settings"));
        AutomationProperties.SetName(DismissAllBtn, Strings.T("DismissAllTooltip"));
        AutomationProperties.SetName(AllowAllBtn, Strings.T("AllowAllAlways"));
        AutomationProperties.SetName(BlockAllBtn, Strings.T("BlockAllPrompts"));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RecalculateLayoutAndPosition();
        if (IsPreview) return;
        if (_autoBlockEnabled && !_countdownTimer.IsEnabled)
            _countdownTimer.Start();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        e.Handled = true;
        DismissAll();
    }

    public void AddCard(ConnectionEvent ev)
    {
        var card = new PromptCardControl(ev, IsPreview);
        card.DecisionMade += OnCardDecisionMade;
        _cards.Add(card);
        CardsContainer.Children.Add(card);

        // Reset countdown to give full time for newly added items
        if (_autoBlockEnabled && !IsPreview)
        {
            _remaining = Math.Clamp(App.Settings.PopupAutoBlockSeconds, 15, 3600);
            UpdateCountdownLabel();
        }

        UpdateStackHeader();
        RecalculateLayoutAndPosition();
    }

    private void OnCardDecisionMade(PromptCardControl card, PopupDecision decision)
    {
        RemoveCard(card, decision, timedOut: false);
    }

    public void RemoveCard(PromptCardControl card, PopupDecision decision, bool timedOut = false)
    {
        card.DecisionMade -= OnCardDecisionMade;
        _cards.Remove(card);
        CardsContainer.Children.Remove(card);

        CardResolved?.Invoke(card, decision, timedOut);

        if (_cards.Count == 0)
        {
            _countdownTimer.Stop();
            Close();
            return;
        }

        UpdateStackHeader();
        RecalculateLayoutAndPosition();
    }

    private void UpdateStackHeader()
    {
        var count = _cards.Count;
        if (count > 1)
        {
            CountBadge.Visibility = Visibility.Visible;
            CountBadgeLbl.Text = count.ToString();
            BulkActionsPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CountBadge.Visibility = Visibility.Collapsed;
            BulkActionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    public void RecalculateLayoutAndPosition()
    {
        UpdateLayout();

        // Calculate card stack height dynamically
        double headerH = 46;
        double margins = 24;
        double cardH = 220; // approximate baseline card height

        int visibleCount = Math.Min(Math.Max(1, _cards.Count), MaxVisibleCards);
        double targetHeight = headerH + margins + (visibleCount * cardH);

        // Cap height so it fits comfortably on 1080p and laptop screens
        Height = Math.Min(targetHeight, 560);
        CardsScrollViewer.MaxHeight = Height - headerH - margins + 8;

        var pos = App.Settings.NotificationPosition;
        var mon = App.Settings.NotificationMonitor;
        PopupWindowHelper.PositionWindow(this, pos, mon, explicitStackIndex: 0);
    }

    private void TickCountdown()
    {
        if (_countdownPaused) return;
        _remaining--;
        if (_remaining <= 0)
        {
            _countdownTimer.Stop();
            // Timed out: auto-block all active cards in batch
            var list = _cards.ToList();
            _cards.Clear();
            CardsContainer.Children.Clear();
            Close();

            foreach (var card in list)
            {
                card.DecisionMade -= OnCardDecisionMade;
                CardResolved?.Invoke(card, PopupDecision.BlockAlways, true);
            }
            return;
        }
        UpdateCountdownLabel();
    }

    private void UpdateCountdownLabel()
    {
        var clock = TimeSpan.FromSeconds(Math.Max(0, _remaining)).ToString(@"m\:ss");
        CountdownLbl.Text = clock;
    }

    private void AllowAll_Click(object sender, RoutedEventArgs e)
    {
        var list = _cards.ToList();
        _cards.Clear();
        CardsContainer.Children.Clear();
        _countdownTimer.Stop();
        Close();

        foreach (var card in list)
        {
            card.DecisionMade -= OnCardDecisionMade;
            CardResolved?.Invoke(card, PopupDecision.AllowAlways, false);
        }
    }

    private void BlockAll_Click(object sender, RoutedEventArgs e)
    {
        var list = _cards.ToList();
        _cards.Clear();
        CardsContainer.Children.Clear();
        _countdownTimer.Stop();
        Close();

        foreach (var card in list)
        {
            card.DecisionMade -= OnCardDecisionMade;
            CardResolved?.Invoke(card, PopupDecision.BlockAlways, false);
        }
    }

    private void DismissAll_Click(object sender, RoutedEventArgs e) => DismissAll();

    public void DismissAll()
    {
        var list = _cards.ToList();
        _cards.Clear();
        CardsContainer.Children.Clear();
        _countdownTimer.Stop();
        Close();

        foreach (var card in list)
        {
            card.DecisionMade -= OnCardDecisionMade;
            CardResolved?.Invoke(card, PopupDecision.Dismiss, false);
        }
        if (IsPreview)
        {
            DismissPreview();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow mw)
            return;
        var wasTop = Topmost;
        Topmost = false;
        try { mw.OpenSettings(); }
        finally { Topmost = wasTop; }
    }

    public static void ShowPreview(PopupPosition position, int monitorIndex)
    {
        DismissPreview();

        var ev = new ConnectionEvent
        {
            AppPath = @"C:\Program Files\Demo\DemoApp.exe",
            RemoteAddress = "142.250.190.46",
            RemotePort = 443,
            Protocol = "TCP",
            Direction = Direction.Outbound,
            ProcessId = 1337
        };

        var win = new PromptStackWindow(isPreview: true);
        _activePreview = win;
        win.AddCard(ev);
        win.Show();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _previewTimer.Tick += (_, _) => DismissPreview();
        _previewTimer.Start();
    }

    public static void DismissPreview()
    {
        if (_previewTimer != null)
        {
            _previewTimer.Stop();
            _previewTimer = null;
        }
        if (_activePreview != null)
        {
            try { _activePreview.Close(); } catch { }
            _activePreview = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        base.OnClosed(e);
        if (_activePreview == this) _activePreview = null;
    }
}

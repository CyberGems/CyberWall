using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.UI.Converters;
using CyberWall.UI.Services;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using Path = System.IO.Path;
using WpfPath = System.Windows.Shapes.Path;

namespace CyberWall.UI.Popup;

public enum ToastBadgeType
{
    Info,
    Success,
    Warning,
    Danger
}

public partial class AppInfoToast : Window
{
    private const int MaxVisibleCards = 2;

    private static AppInfoToast? _activeWindow;
    private static readonly PathToIconConverter IconConv = new();

    private sealed class ToastCardEntry
    {
        public required string Title { get; set; }
        public required string Message { get; set; }
        public string? AppPath { get; set; }
        public ToastBadgeType BadgeType { get; set; }
        public string? BadgeText { get; set; }
        public required Border CardRoot { get; init; }
        public required Grid CardHeaderRow { get; init; }
        public required Border CardBadgeBorder { get; init; }
        public required TextBlock CardBadgeLbl { get; init; }
        public required TextBlock CardTitleLbl { get; init; }
        public required Grid IconHost { get; init; }
        public required TextBlock DescLbl { get; init; }
        public required Grid ProcessActionsPanel { get; init; }
        public required Border FilePillBorder { get; init; }
        public required TextBlock PathLbl { get; init; }
        public required Button CopyPathBtn { get; init; }
    }

    private readonly List<ToastCardEntry> _items = new();
    private readonly DispatcherTimer _autoCloseTimer;

    public AppInfoToast()
    {
        InitializeComponent();
        Title = Strings.T("WindowNotification");

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) => RecalculateLayoutAndPosition();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            CloseToast();
        };

        TopBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); } catch { }
        };

        CloseBtn.ToolTip = Strings.T("Close");
        AutomationProperties.SetName(CloseBtn, Strings.T("Close"));
        SettingsBtn.ToolTip = Strings.T("Settings");
        AutomationProperties.SetName(SettingsBtn, Strings.T("Settings"));

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            CloseToast();
        };
        _autoCloseTimer.Start();

        MouseEnter += (_, _) => _autoCloseTimer.Stop();
        MouseLeave += (_, _) =>
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Start();
        };
    }

    public AppInfoToast(string title, string message, string? appPath = null, ToastBadgeType badgeType = ToastBadgeType.Info, string? badgeText = null)
        : this()
    {
        AddOrUpdateItem(title, message, appPath, badgeType, badgeText);
    }

    public void AddOrUpdateItem(string title, string message, string? appPath, ToastBadgeType badgeType, string? badgeText)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? Strings.T("AppInfoMonitor") : title;

        // If an item for the same app and title is already in the stack, update it in place
        var existing = !string.IsNullOrWhiteSpace(appPath)
            ? _items.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.AppPath) &&
                string.Equals(x.AppPath, appPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase))
            : _items.FirstOrDefault(x =>
                string.IsNullOrWhiteSpace(x.AppPath) &&
                string.Equals(x.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Title = normalizedTitle;
            existing.Message = message;
            existing.AppPath = appPath;
            existing.BadgeType = badgeType;
            existing.BadgeText = badgeText;
            PopulateCardEntry(existing);
        }
        else
        {
            var entry = CreateCardEntry(normalizedTitle, message, appPath, badgeType, badgeText);
            _items.Add(entry);
            CardsContainer.Children.Add(entry.CardRoot);
        }

        // Reset countdown timer so user has full time to read newly arrived notification
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();

        UpdateStackChrome();
        if (IsLoaded)
        {
            RecalculateLayoutAndPosition();
        }
    }

    private ToastCardEntry CreateCardEntry(string title, string message, string? appPath, ToastBadgeType badgeType, string? badgeText)
    {
        var cardRoot = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        cardRoot.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        cardRoot.SetResourceReference(Border.BorderBrushProperty, "BorderLightBrush");

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Card-level header (shown when stack has > 1 cards so each card has its badge, title & dismiss X)
        var cardHeaderRow = new Grid { Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
        cardHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cardBadgeBorder = new Border
        {
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 1.5, 6, 1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var cardBadgeLbl = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        cardBadgeBorder.Child = cardBadgeLbl;
        Grid.SetColumn(cardBadgeBorder, 0);
        cardHeaderRow.Children.Add(cardBadgeBorder);

        var cardTitleLbl = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        cardTitleLbl.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetColumn(cardTitleLbl, 1);
        cardHeaderRow.Children.Add(cardTitleLbl);

        var cardDismissBtn = new Button
        {
            Style = (Style)FindResource("ToastHeaderBtn"),
            Width = 22,
            Height = 22,
            ToolTip = Strings.T("Close")
        };
        AutomationProperties.SetName(cardDismissBtn, Strings.T("Close"));
        var dismissPath = new WpfPath
        {
            Data = Geometry.Parse("M 1 1 L 9 9 M 9 1 L 1 9"),
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 8.5,
            Height = 8.5,
            Stretch = Stretch.Uniform
        };
        dismissPath.SetResourceReference(Shape.StrokeProperty, "SubTextBrush");
        cardDismissBtn.Content = dismissPath;
        Grid.SetColumn(cardDismissBtn, 2);
        cardHeaderRow.Children.Add(cardDismissBtn);

        Grid.SetRow(cardHeaderRow, 0);
        outerGrid.Children.Add(cardHeaderRow);

        // Main card body (Icon + Message + Process Actions)
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconSquircle = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            SnapsToDevicePixels = true
        };
        iconSquircle.SetResourceReference(Border.BackgroundProperty, "CardSecondaryBrush");
        iconSquircle.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var iconHost = new Grid
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconSquircle.Child = iconHost;
        Grid.SetColumn(iconSquircle, 0);
        bodyGrid.Children.Add(iconSquircle);

        var rightGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var descLbl = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 17
        };
        descLbl.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetRow(descLbl, 0);
        rightGrid.Children.Add(descLbl);

        var processActionsPanel = new Grid { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        processActionsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        processActionsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var filePillBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 3, 7, 3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        filePillBorder.SetResourceReference(Border.BackgroundProperty, "CardSecondaryBrush");
        filePillBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var filePillStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var fileIconPath = new WpfPath
        {
            Data = Geometry.Parse("M 4 2 H 13 L 18 7 V 20 C 18 21.1 17.1 22 16 22 H 4 C 2.9 22 2 21.1 2 20 V 4 C 2 2.9 2.9 2 4 2 Z M 13 2 V 7 H 18"),
            StrokeThickness = 1.2,
            Width = 10,
            Height = 11,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Fill = System.Windows.Media.Brushes.Transparent
        };
        fileIconPath.SetResourceReference(Shape.StrokeProperty, "SubTextBrush");
        filePillStack.Children.Add(fileIconPath);

        var pathLbl = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        pathLbl.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        filePillStack.Children.Add(pathLbl);
        filePillBorder.Child = filePillStack;
        Grid.SetColumn(filePillBorder, 0);
        processActionsPanel.Children.Add(filePillBorder);

        var actionsStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0)
        };

        var searchBtn = CreateActionIconButton(
            "M6,1.2 C3.4,1.2 1.2,3.4 1.2,6 C1.2,8.6 3.4,10.8 6,10.8 C7.1,10.8 8.1,10.4 8.9,9.8 L11.1,12 L12,11.1 L9.8,8.9 C10.4,8.1 10.8,7.1 10.8,6 C10.8,3.4 8.6,1.2 6,1.2 Z M6,2.6 A3.4,3.4 0 1 0 6,9.4 A3.4,3.4 0 1 0 6,2.6 Z",
            Strings.T("SearchOnline"),
            new Thickness(0, 0, 4, 0));
        var copyPathBtn = CreateActionIconButton(
            "M4.2,3.4 H9.6 V9.2 H4.2 Z M6.2,1.6 H11.6 V7.4 H10.2 V3 H6.2 Z",
            Strings.T("CopyPath"),
            new Thickness(0, 0, 4, 0));
        var openFolderBtn = CreateActionIconButton(
            "M1,3 L1,10 L11,10 L11,5 L6.2,5 L5.2,3 Z",
            Strings.T("OpenFolder"),
            new Thickness(0));

        actionsStack.Children.Add(searchBtn);
        actionsStack.Children.Add(copyPathBtn);
        actionsStack.Children.Add(openFolderBtn);
        Grid.SetColumn(actionsStack, 1);
        processActionsPanel.Children.Add(actionsStack);

        Grid.SetRow(processActionsPanel, 1);
        rightGrid.Children.Add(processActionsPanel);

        Grid.SetColumn(rightGrid, 1);
        bodyGrid.Children.Add(rightGrid);

        Grid.SetRow(bodyGrid, 1);
        outerGrid.Children.Add(bodyGrid);
        cardRoot.Child = outerGrid;

        var entry = new ToastCardEntry
        {
            Title = title,
            Message = message,
            AppPath = appPath,
            BadgeType = badgeType,
            BadgeText = badgeText,
            CardRoot = cardRoot,
            CardHeaderRow = cardHeaderRow,
            CardBadgeBorder = cardBadgeBorder,
            CardBadgeLbl = cardBadgeLbl,
            CardTitleLbl = cardTitleLbl,
            IconHost = iconHost,
            DescLbl = descLbl,
            ProcessActionsPanel = processActionsPanel,
            FilePillBorder = filePillBorder,
            PathLbl = pathLbl,
            CopyPathBtn = copyPathBtn
        };

        cardDismissBtn.Click += (_, e) =>
        {
            e.Handled = true;
            RemoveCardEntry(entry);
        };
        searchBtn.Click += (_, e) =>
        {
            e.Handled = true;
            SearchProcess(entry.AppPath);
        };
        copyPathBtn.Click += (_, e) =>
        {
            e.Handled = true;
            CopyProcessPath(entry);
        };
        openFolderBtn.Click += (_, e) =>
        {
            e.Handled = true;
            OpenProcessFolder(entry.AppPath);
        };
        cardRoot.MouseLeftButtonDown += OnBodyClick;

        PopulateCardEntry(entry);
        return entry;
    }

    private Button CreateActionIconButton(string pathData, string tooltip, Thickness margin)
    {
        var btn = new Button
        {
            Style = (Style)FindResource("ToastIconButton"),
            ToolTip = tooltip,
            Margin = margin
        };
        AutomationProperties.SetName(btn, tooltip);
        var path = new WpfPath
        {
            Data = Geometry.Parse(pathData),
            Stretch = Stretch.Uniform,
            Width = 10,
            Height = 10
        };
        path.SetResourceReference(Shape.FillProperty, "SubTextBrush");
        btn.Content = path;
        return btn;
    }

    private void PopulateCardEntry(ToastCardEntry entry)
    {
        entry.CardTitleLbl.Text = entry.Title;
        ApplyBadgeToElements(entry.CardBadgeBorder, entry.CardBadgeLbl, entry.BadgeType, entry.BadgeText);
        PopulateIconHost(entry.IconHost, entry.AppPath, entry.BadgeType);
        FormatDescriptionText(entry.DescLbl, entry.Message);

        if (!string.IsNullOrWhiteSpace(entry.AppPath))
        {
            entry.ProcessActionsPanel.Visibility = Visibility.Visible;
            entry.PathLbl.Text = Path.GetFileName(entry.AppPath);
            entry.FilePillBorder.ToolTip = entry.AppPath;
            entry.CopyPathBtn.ToolTip = Strings.T("CopyPath");
        }
        else
        {
            entry.ProcessActionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void FormatDescriptionText(TextBlock descLbl, string message)
    {
        descLbl.Inlines.Clear();
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var textBrush = (Brush)FindResource("TextBrush");
        var subTextBrush = (Brush)FindResource("SubTextBrush");

        // Match either "App: details" or "App cambió de versión..." / "App version changed..." / "App executable changed."
        int splitIdx = -1;
        bool includeColon = false;
        if (message.IndexOf(':') is int colonIdx && colonIdx > 0 && colonIdx < 45)
        {
            splitIdx = colonIdx;
            includeColon = true;
        }
        else
        {
            string[] markers = { " cambió de versión", " version changed", " executable changed" };
            foreach (var marker in markers)
            {
                int mIdx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (mIdx > 0)
                {
                    splitIdx = mIdx;
                    break;
                }
            }
        }

        if (splitIdx > 0)
        {
            var appName = includeColon ? message[..(splitIdx + 1)] : message[..splitIdx];
            var rest = includeColon ? message[(splitIdx + 1)..] : message[splitIdx..];
            descLbl.Inlines.Add(new Bold(new Run(appName)) { Foreground = textBrush });
            descLbl.Inlines.Add(new Run(rest) { Foreground = subTextBrush });
        }
        else
        {
            descLbl.Text = message;
        }
    }

    private void RemoveCardEntry(ToastCardEntry entry)
    {
        _items.Remove(entry);
        CardsContainer.Children.Remove(entry.CardRoot);

        if (_items.Count == 0)
        {
            _autoCloseTimer.Stop();
            CloseToast();
            return;
        }

        UpdateStackChrome();
        RecalculateLayoutAndPosition();
    }

    private void UpdateStackChrome()
    {
        int count = _items.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            _items[i].CardRoot.Margin = i < count - 1 ? new Thickness(0, 0, 0, 8) : new Thickness(0);
            _items[i].CardHeaderRow.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (count == 1)
        {
            var single = _items[0];
            TitleLbl.Text = single.Title;
            BadgeBorder.Visibility = Visibility.Visible;
            ApplyBadgeToElements(BadgeBorder, BadgeLbl, single.BadgeType, single.BadgeText);
            CountBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            bool sameTitle = _items.All(x => string.Equals(x.Title, _items[0].Title, StringComparison.OrdinalIgnoreCase));
            TitleLbl.Text = sameTitle ? _items[0].Title : Strings.T("AppInfoMonitor");
            BadgeBorder.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Visible;
            CountBadgeLbl.Text = count.ToString();
        }
    }

    public void RecalculateLayoutAndPosition()
    {
        if (_items.Count <= MaxVisibleCards)
        {
            CardsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            CardsScrollViewer.MaxHeight = double.PositiveInfinity;
            CardsScrollViewer.Padding = new Thickness(0);
        }
        else
        {
            CardsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            CardsScrollViewer.Padding = new Thickness(0, 0, 4, 0);

            double availW = CardsScrollViewer.ActualWidth > 10 ? CardsScrollViewer.ActualWidth - 18 : 380;
            double visibleHeight = 0;
            for (int i = 0; i < MaxVisibleCards && i < _items.Count; i++)
            {
                _items[i].CardRoot.Measure(new System.Windows.Size(availW, double.PositiveInfinity));
                visibleHeight += _items[i].CardRoot.DesiredSize.Height;
            }
            CardsScrollViewer.MaxHeight = visibleHeight > 60 ? visibleHeight : 230;
        }

        SizeToContent = SizeToContent.Height;
        UpdateLayout();
        PopupWindowHelper.PositionWindow(this, App.Settings.NotificationPosition, App.Settings.NotificationMonitor);
    }

    private static void ApplyBadgeToElements(Border badgeBorder, TextBlock badgeLbl, ToastBadgeType type, string? customBadge)
    {
        switch (type)
        {
            case ToastBadgeType.Success:
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeAllowBgBrush");
                badgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeAllowFgBrush");
                badgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeAllowFgBrush");
                badgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "ACTIVO" : "ONLINE");
                break;
            case ToastBadgeType.Danger:
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeBlockBgBrush");
                badgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeBlockFgBrush");
                badgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeBlockFgBrush");
                badgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "ALERTA" : "OFFLINE");
                break;
            case ToastBadgeType.Warning:
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "BadgeWarnBgBrush");
                badgeBorder.SetResourceReference(Border.BorderBrushProperty, "BadgeWarnFgBrush");
                badgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "BadgeWarnFgBrush");
                badgeLbl.Text = customBadge ?? (Strings.Current == Lang.Es ? "AVISO" : "WARN");
                break;
            case ToastBadgeType.Info:
            default:
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "SelectionBgBrush");
                badgeBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                badgeLbl.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                badgeLbl.Text = customBadge ?? "INFO";
                break;
        }
    }

    private static void PopulateIconHost(Grid iconHost, string? appPath, ToastBadgeType type)
    {
        iconHost.Children.Clear();

        if (!string.IsNullOrWhiteSpace(appPath))
        {
            var icon = IconConv.Convert(appPath, typeof(ImageSource), null!, null!) as ImageSource;
            if (icon != null)
            {
                var img = new Image
                {
                    Width = 28,
                    Height = 28,
                    Source = icon
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                iconHost.Children.Add(img);
                return;
            }
        }

        WpfPath vectorIcon;
        switch (type)
        {
            case ToastBadgeType.Danger:
                vectorIcon = new WpfPath
                {
                    Data = Geometry.Parse("M 12,19 L 12,19.01 M 8.5,15.5 C 10.5,13.8 13.5,13.8 15.5,15.5 M 5.5,12 C 9.2,8.5 14.8,8.5 18.5,12 M 2.5,8.5 C 8,3.5 16,3.5 21.5,8.5 M 3,3 L 21,21"),
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stretch = Stretch.Uniform,
                    Width = 18,
                    Height = 18,
                    Fill = System.Windows.Media.Brushes.Transparent
                };
                vectorIcon.SetResourceReference(Shape.StrokeProperty, "BadgeBlockFgBrush");
                break;
            case ToastBadgeType.Success:
                vectorIcon = new WpfPath
                {
                    Data = Geometry.Parse("M 12,19 L 12,19.01 M 8.5,15.5 C 10.5,13.8 13.5,13.8 15.5,15.5 M 5.5,12 C 9.2,8.5 14.8,8.5 18.5,12 M 2.5,8.5 C 8,3.5 16,3.5 21.5,8.5"),
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stretch = Stretch.Uniform,
                    Width = 18,
                    Height = 18,
                    Fill = System.Windows.Media.Brushes.Transparent
                };
                vectorIcon.SetResourceReference(Shape.StrokeProperty, "BadgeAllowFgBrush");
                break;
            case ToastBadgeType.Warning:
                vectorIcon = new WpfPath
                {
                    Data = Geometry.Parse("M 12,2 L 4,5 L 4,11 C 4,16.5 7.5,21.3 12,22.5 C 16.5,21.3 20,16.5 20,11 L 20,5 Z M 12,7 L 12,13 M 12,16 L 12,17"),
                    StrokeThickness = 1.4,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Stretch = Stretch.Uniform,
                    Width = 18,
                    Height = 18,
                    Fill = System.Windows.Media.Brushes.Transparent
                };
                vectorIcon.SetResourceReference(Shape.StrokeProperty, "BadgeWarnFgBrush");
                break;
            default:
                vectorIcon = new WpfPath
                {
                    Data = Geometry.Parse("M 12 2 C 6.48 2 2 6.48 2 12 C 2 17.52 6.48 22 12 22 C 17.52 22 22 17.52 22 12 C 22 6.48 17.52 2 12 2 Z M 12 7 A 1.25 1.25 0 1 1 12 9.5 A 1.25 1.25 0 1 1 12 7 Z M 11 11 L 13 11 L 13 17 L 11 17 Z"),
                    Stretch = Stretch.Uniform,
                    Width = 18,
                    Height = 18
                };
                vectorIcon.SetResourceReference(Shape.FillProperty, "AccentBrush");
                break;
        }

        iconHost.Children.Add(vectorIcon);
    }

    public static void ShowToast(string title, string message, string? appPath = null, ToastBadgeType badgeType = ToastBadgeType.Info, string? badgeText = null)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        app.Dispatcher.BeginInvoke(() =>
        {
            if (_activeWindow == null || !_activeWindow.IsLoaded)
            {
                _activeWindow = new AppInfoToast();
                _activeWindow.Closed += (_, _) =>
                {
                    if (_activeWindow != null && !_activeWindow.IsLoaded)
                        _activeWindow = null;
                };
                _activeWindow.AddOrUpdateItem(title, message, appPath, badgeType, badgeText);
                _activeWindow.Show();
            }
            else
            {
                _activeWindow.AddOrUpdateItem(title, message, appPath, badgeType, badgeText);
            }
        });
    }

    public static void CloseAll()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(() =>
        {
            _activeWindow?.CloseToast();
        });
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) != null) return;
        _autoCloseTimer.Stop();
        CloseToast();
        OpenNotifications();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        CloseToast();
        if (System.Windows.Application.Current.MainWindow is MainWindow mw)
        {
            mw.Dispatcher.Invoke(() => mw.OpenSettings());
        }
    }

    private static void SearchProcess(string? appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return;
        try
        {
            var fn = Path.GetFileName(appPath);
            Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString($"{fn} process"))
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static void CopyProcessPath(ToastCardEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.AppPath)) return;
        try
        {
            System.Windows.Clipboard.SetText(entry.AppPath);
            entry.CopyPathBtn.ToolTip = Strings.T("CopiedToClipboard");
        }
        catch { }
    }

    private static void OpenProcessFolder(string? appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return;
        try
        {
            if (File.Exists(appPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{appPath}\"") { UseShellExecute = true });
                return;
            }
            var dir = Path.GetDirectoryName(appPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        CloseToast();
    }

    private void CloseToast()
    {
        if (_activeWindow == this)
        {
            _activeWindow = null;
        }
        try { Close(); } catch { }
    }

    private static void OpenNotifications()
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow mw) return;
        mw.ShowNotifications();
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T target) return target;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

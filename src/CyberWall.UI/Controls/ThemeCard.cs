using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using RadioButton = System.Windows.Controls.RadioButton;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace CyberWall.UI.Controls;

public sealed class ThemeCard : RadioButton
{
    private const double DefaultCardWidth = 120;
    private const double PreviewAspect = 62.0 / 96.0;
    private const double CardPadding = 8;

    private double CardW => CardWidth;
    private double PreviewW => CardWidth - CardPadding * 2;
    private double PreviewH => PreviewW * PreviewAspect;

    private Border _card = null!;
    private Border _glow = null!;
    private TextBlock _caption = null!;

    public static readonly DependencyProperty ThemeModeProperty =
        DependencyProperty.Register(nameof(ThemeMode), typeof(AppTheme), typeof(ThemeCard),
            new PropertyMetadata(AppTheme.CyberWall, OnVisualChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(ThemeCard),
            new PropertyMetadata("CyberWall", OnVisualChanged));

    public static readonly DependencyProperty CardWidthProperty =
        DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(ThemeCard),
            new PropertyMetadata(DefaultCardWidth, OnVisualChanged));

    public AppTheme ThemeMode
    {
        get => (AppTheme)GetValue(ThemeModeProperty);
        set => SetValue(ThemeModeProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    public ThemeCard()
    {
        Cursor = System.Windows.Input.Cursors.Hand;
        FocusVisualStyle = null;
        GroupName = "ThemeGroup";
        HorizontalAlignment = WpfHorizontalAlignment.Center;
        VerticalAlignment = WpfVerticalAlignment.Top;
        Template = (ControlTemplate)XamlReader.Parse(
            "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "TargetType=\"RadioButton\"><ContentPresenter/></ControlTemplate>");

        Content = Build();
        UpdateSelectionVisual();

        Checked += (_, _) => UpdateSelectionVisual();
        Unchecked += (_, _) => UpdateSelectionVisual();
        MouseEnter += (_, _) => UpdateSelectionVisual();
        MouseLeave += (_, _) => UpdateSelectionVisual();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThemeCard c) c.Rebuild();
    }

    public void RefreshCaption(string text)
    {
        Caption = text;
        if (_caption != null) _caption.Text = text;
    }

    private void Rebuild()
    {
        Content = Build();
        UpdateSelectionVisual();
    }

    private FrameworkElement Build()
    {
        var stack = new StackPanel { Width = CardW };

        _glow = new Border
        {
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(-1),
            Background = Brushes.Transparent
        };

        _card = new Border
        {
            Width = CardW,
            CornerRadius = new CornerRadius(11),
            Background = MetallicBrush(),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255)),
            Padding = new Thickness(CardPadding),
            Child = BuildPreview()
        };

        var cardHost = new Grid();
        cardHost.Children.Add(_glow);
        cardHost.Children.Add(_card);

        _caption = new TextBlock
        {
            Text = Caption,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.Medium
        };

        stack.Children.Add(cardHost);
        stack.Children.Add(_caption);
        return stack;
    }

    private static Brush MetallicBrush()
    {
        var g = new LinearGradientBrush { StartPoint = new WpfPoint(0.1, 0), EndPoint = new WpfPoint(0.9, 1) };
        g.GradientStops.Add(new GradientStop(WpfColor.FromRgb(45, 50, 60), 0.0));
        g.GradientStops.Add(new GradientStop(WpfColor.FromRgb(30, 35, 42), 0.5));
        g.GradientStops.Add(new GradientStop(WpfColor.FromRgb(22, 26, 32), 1.0));
        g.Freeze();
        return g;
    }

    private FrameworkElement BuildPreview()
    {
        var p = ThemeManager.Get(ThemeMode);
        var bg = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(p.Bg)!;

        var window = new Border
        {
            Width = PreviewW,
            Height = PreviewH,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(bg),
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = WpfVerticalAlignment.Center,
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 2,
                Opacity = 0.4,
                Color = Colors.Black
            }
        };

        window.Child = BuildWindowContent(p);
        return window;
    }

    private FrameworkElement BuildWindowContent(ThemeManager.Palette p)
    {
        var subText = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(p.SubText)!;
        var text = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(p.Text)!;
        var accent = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(p.Accent)!;

        double inset = PreviewW * 0.08;
        var root = new Grid { Margin = new Thickness(inset, inset * 0.8, inset, inset * 0.8) };

        var lines = new StackPanel { VerticalAlignment = WpfVerticalAlignment.Center };

        var dotTop = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(accent),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            VerticalAlignment = WpfVerticalAlignment.Top
        };

        double inner = PreviewW - inset * 2;
        lines.Children.Add(Line(text, 0.9, inner * 0.5, 10));
        lines.Children.Add(Line(subText, 0.65, inner * 0.7, 5));
        lines.Children.Add(Line(subText, 0.45, inner * 0.4, 5));

        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = new SolidColorBrush(accent),
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            VerticalAlignment = WpfVerticalAlignment.Bottom
        };

        root.Children.Add(lines);
        root.Children.Add(dotTop);
        root.Children.Add(dot);
        return root;
    }

    private static FrameworkElement Line(WpfColor color, double opacity, double width, double topMargin)
    {
        return new Border
        {
            Height = 3.5,
            Width = width,
            CornerRadius = new CornerRadius(1.75),
            Background = new SolidColorBrush(color) { Opacity = opacity },
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Margin = new Thickness(0, topMargin, 0, 0)
        };
    }

    private void UpdateSelectionVisual()
    {
        if (_card == null) return;

        var p = ThemeManager.Get(ThemeMode);
        var accent = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(p.Accent)!;

        bool selected = IsChecked == true;
        bool hover = IsMouseOver;

        if (selected)
        {
            _card.BorderBrush = new SolidColorBrush(accent);
            _card.BorderThickness = new Thickness(1.8);
            _glow.Background = new SolidColorBrush(accent);
            _glow.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.55,
                Color = accent
            };
            _glow.Opacity = 1;
            _caption.FontWeight = FontWeights.Bold;
            _caption.Foreground = new SolidColorBrush(WpfColor.FromRgb(240, 245, 255));
        }
        else
        {
            _card.BorderBrush = new SolidColorBrush(
                hover ? WpfColor.FromArgb(100, 255, 255, 255) : WpfColor.FromArgb(40, 255, 255, 255));
            _card.BorderThickness = new Thickness(1);
            _glow.Effect = null;
            _glow.Background = Brushes.Transparent;
            _glow.Opacity = 0;
            _caption.FontWeight = FontWeights.Normal;
            _caption.Foreground = new SolidColorBrush(WpfColor.FromRgb(140, 150, 165));
        }
    }
}

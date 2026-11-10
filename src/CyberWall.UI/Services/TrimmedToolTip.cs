using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CyberWall.UI.Services;

/// <summary>
/// Attached property for TextBlock that suppresses tooltips unless the text is actually
/// trimmed/truncated with an ellipsis, preventing redundant tooltips for fully visible text.
/// </summary>
public static class TrimmedToolTip
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(TrimmedToolTip),
            new FrameworkPropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        if ((bool)e.NewValue)
        {
            tb.ToolTipOpening += OnToolTipOpening;
        }
        else
        {
            tb.ToolTipOpening -= OnToolTipOpening;
        }
    }

    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not TextBlock tb) return;

        if (!IsTextTrimmed(tb))
        {
            e.Handled = true; // Suppress tooltip if text is completely visible
        }
    }

    private static bool IsTextTrimmed(TextBlock tb)
    {
        if (tb.ActualWidth <= 0) return false;

        string? text = tb.Text;
        if (string.IsNullOrEmpty(text) && tb.Inlines.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var inline in tb.Inlines)
            {
                if (inline is Run run) sb.Append(run.Text);
            }
            text = sb.ToString();
        }

        if (string.IsNullOrEmpty(text)) return false;

        var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
        var dpi = VisualTreeHelper.GetDpi(tb).PixelsPerDip;
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            tb.FontSize,
            System.Windows.Media.Brushes.Black,
            dpi);

        return formattedText.WidthIncludingTrailingWhitespace > (tb.ActualWidth + 0.5);
    }
}


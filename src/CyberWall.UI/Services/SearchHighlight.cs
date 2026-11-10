using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace CyberWall.UI.Services;

/// <summary>
/// Provides attached properties on TextBlock to highlight matching search substrings
/// with the application accent color and bold weight in real time.
/// </summary>
public static class SearchHighlight
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(SearchHighlight),
            new FrameworkPropertyMetadata(string.Empty, OnHighlightChanged));

    public static readonly DependencyProperty SearchTermProperty =
        DependencyProperty.RegisterAttached(
            "SearchTerm",
            typeof(string),
            typeof(SearchHighlight),
            new FrameworkPropertyMetadata(string.Empty, OnHighlightChanged));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    public static string GetSearchTerm(DependencyObject obj) => (string)obj.GetValue(SearchTermProperty);
    public static void SetSearchTerm(DependencyObject obj, string value) => obj.SetValue(SearchTermProperty, value);

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        var fullText = GetText(tb);
        var query = GetSearchTerm(tb);

        if (string.IsNullOrEmpty(fullText))
        {
            tb.Inlines.Clear();
            tb.Text = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            tb.Inlines.Clear();
            tb.Text = fullText;
            return;
        }

        var trimmedQuery = query.Trim();
        int index = fullText.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            tb.Inlines.Clear();
            tb.Text = fullText;
            return;
        }

        tb.Text = null;
        tb.Inlines.Clear();

        int lastIndex = 0;
        var accentBrush = (WpfBrush?)WpfApplication.Current?.TryFindResource("AccentBrush") ?? WpfBrushes.Cyan;

        while (index >= 0)
        {
            if (index > lastIndex)
            {
                tb.Inlines.Add(new Run(fullText[lastIndex..index]));
            }

            var matchRun = new Run(fullText.Substring(index, trimmedQuery.Length))
            {
                Foreground = accentBrush,
                FontWeight = FontWeights.Bold
            };
            tb.Inlines.Add(matchRun);

            lastIndex = index + trimmedQuery.Length;
            if (lastIndex >= fullText.Length) break;
            index = fullText.IndexOf(trimmedQuery, lastIndex, StringComparison.OrdinalIgnoreCase);
        }

        if (lastIndex < fullText.Length)
        {
            tb.Inlines.Add(new Run(fullText[lastIndex..]));
        }
    }
}

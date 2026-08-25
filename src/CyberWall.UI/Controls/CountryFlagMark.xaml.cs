using System.Windows;
using CyberWall.Common.Geo;

namespace CyberWall.UI.Controls;

public partial class CountryFlagMark : System.Windows.Controls.UserControl
{
    public CountryFlagMark()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public static readonly DependencyProperty CountryCodeProperty =
        DependencyProperty.Register(nameof(CountryCode), typeof(string), typeof(CountryFlagMark),
            new PropertyMetadata("", (d, _) => ((CountryFlagMark)d).Refresh()));

    public static readonly DependencyProperty HasCountryProperty =
        DependencyProperty.Register(nameof(HasCountry), typeof(bool), typeof(CountryFlagMark),
            new PropertyMetadata(false, (d, _) => ((CountryFlagMark)d).Refresh()));

    public string CountryCode
    {
        get => (string)GetValue(CountryCodeProperty);
        set => SetValue(CountryCodeProperty, value);
    }

    public bool HasCountry
    {
        get => (bool)GetValue(HasCountryProperty);
        set => SetValue(HasCountryProperty, value);
    }

    public void Apply(GeoResult result)
    {
        HasCountry = result.HasCountry;
        CountryCode = result.HasCountry ? result.Iso2 ?? "" : "";
        ToolTip = CountryDisplay.Label(result);
        Refresh();
    }

    private void Refresh()
    {
        if (FlagImage == null || GlobePath == null) return;
        var src = HasCountry ? FlagPainter.Create(CountryCode) : null;
        if (src != null)
        {
            FlagImage.Source = src;
            FlagImage.Visibility = Visibility.Visible;
            GlobePath.Visibility = Visibility.Collapsed;
        }
        else
        {
            FlagImage.Source = null;
            FlagImage.Visibility = Visibility.Collapsed;
            GlobePath.Visibility = Visibility.Visible;
        }
    }
}

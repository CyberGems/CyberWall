using System.Globalization;
using CyberWall.Common.I18n;

namespace CyberWall.Common.Geo;

public static class CountryDisplay
{
    public static string Name(string iso)
    {
        try
        {
            var culture = Strings.Current == Lang.Es ? "es" : "en";
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return new RegionInfo(iso).DisplayName;
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }
        catch
        {
            return iso;
        }
    }

    public static string Label(GeoResult result) => result.Kind switch
    {
        GeoKind.Local => Strings.T("LocalNetwork"),
        GeoKind.Country when result.Iso2 is { Length: 2 } iso => Name(iso),
        _ => Strings.T("UnknownCountry")
    };
}

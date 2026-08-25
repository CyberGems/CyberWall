using CyberWall.Common.Geo;
using CyberWall.Common.Models;

namespace CyberWall.UI;

public sealed class AppRuleRow
{
    public required AppRule Rule { get; init; }
    public GeoResult Geo { get; init; }

    public string AppPath => Rule.AppPath;
    public string DisplayName => Rule.DisplayName;
    public Verdict Verdict => Rule.Verdict;
    public Direction Direction => Rule.Direction;
    public bool HasCountry => Geo.HasCountry;
    public string CountryCode => Geo.HasCountry ? Geo.Iso2 ?? "" : "";
    public string CountryLabel => CountryDisplay.Label(Geo);
}

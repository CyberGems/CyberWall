using System.ComponentModel;
using System.Runtime.CompilerServices;
using CyberWall.Common.Geo;
using CyberWall.Common.Models;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public sealed class AppRuleRow : INotifyPropertyChanged
{
    public required AppRule Rule { get; init; }
    public GeoResult Geo { get; init; }

    private bool _isActiveTraffic;
    private string _activityTooltip = string.Empty;

    public string AppPath => Rule.AppPath;
    public string DisplayName => Rule.DisplayName;
    public Verdict Verdict => Rule.Verdict;
    public Direction Direction => Rule.Direction;
    public Verdict InboundVerdict => Rule.EffectiveInboundVerdict;
    public Verdict OutboundVerdict => Rule.EffectiveOutboundVerdict;
    public bool HasCountry => Geo.HasCountry;
    public string CountryCode => Geo.HasCountry ? Geo.Iso2 ?? "" : "";
    public string CountryLabel => CountryDisplay.Label(Geo);

    public bool IsActiveTraffic
    {
        get => _isActiveTraffic;
        set
        {
            if (_isActiveTraffic != value)
            {
                _isActiveTraffic = value;
                OnPropertyChanged();
            }
        }
    }

    public string ActivityTooltip
    {
        get => _activityTooltip;
        set
        {
            if (_activityTooltip != value)
            {
                _activityTooltip = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateActivity(ProcessTrafficTracker tracker)
    {
        var activity = tracker.GetActivity(AppPath);
        IsActiveTraffic = activity.IsActive;
        ActivityTooltip = tracker.FormatTooltip(AppPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

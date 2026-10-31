using System.ComponentModel;
using System.Runtime.CompilerServices;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.Models;
using CyberWall.UI.Services;

namespace CyberWall.UI;

public sealed class AppRuleRow : INotifyPropertyChanged
{
    public required AppRule Rule { get; init; }
    private GeoResult _geo;
    public GeoResult Geo
    {
        get => _geo;
        init => _geo = value;
    }

    public void UpdateGeo(GeoResult geo)
    {
        if (_geo != geo)
        {
            _geo = geo;
            OnPropertyChanged(nameof(Geo));
            OnPropertyChanged(nameof(HasCountry));
            OnPropertyChanged(nameof(CountryCode));
            OnPropertyChanged(nameof(CountryLabel));
        }
    }

    private ProcessActivityLevel _activityLevel = ProcessActivityLevel.Idle;
    private string _activityTooltip = string.Empty;
    private string _bandwidthSpeedText = string.Empty;
    private bool _hasActiveBandwidth;
    private double _downloadBps;
    private double _uploadBps;

    public string AppPath => Rule.AppPath;
    public string DisplayName => Rule.DisplayName;
    public Verdict Verdict => Rule.Verdict;
    public Direction Direction => Rule.Direction;
    public Verdict InboundVerdict => Rule.EffectiveInboundVerdict;
    public Verdict OutboundVerdict => Rule.EffectiveOutboundVerdict;
    public bool HasCountry => Geo.HasCountry;
    public string CountryCode => Geo.HasCountry ? Geo.Iso2 ?? "" : "";
    public string CountryLabel => CountryDisplay.Label(Geo);

    public ProcessActivityLevel ActivityLevel
    {
        get => _activityLevel;
        set
        {
            if (_activityLevel != value)
            {
                _activityLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsActiveTraffic));
                OnPropertyChanged(nameof(IsBlockedAttempts));
            }
        }
    }

    public bool IsActiveTraffic => ActivityLevel == ProcessActivityLevel.ActiveAllowed;
    public bool IsBlockedAttempts => ActivityLevel == ProcessActivityLevel.BlockedAttempts;

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

    public string BandwidthSpeedText
    {
        get => _bandwidthSpeedText;
        set
        {
            if (_bandwidthSpeedText != value)
            {
                _bandwidthSpeedText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasActiveBandwidth
    {
        get => _hasActiveBandwidth;
        set
        {
            if (_hasActiveBandwidth != value)
            {
                _hasActiveBandwidth = value;
                OnPropertyChanged();
            }
        }
    }

    public double DownloadBps
    {
        get => _downloadBps;
        set
        {
            if (Math.Abs(_downloadBps - value) > 0.01)
            {
                _downloadBps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalBps));
            }
        }
    }

    public double UploadBps
    {
        get => _uploadBps;
        set
        {
            if (Math.Abs(_uploadBps - value) > 0.01)
            {
                _uploadBps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalBps));
            }
        }
    }

    public double TotalBps => _downloadBps + _uploadBps;

    private ProcessActivityLevel _lastTooltipLevel = (ProcessActivityLevel)(-1);
    private int _lastActiveSockets = -1;
    private DateTime _lastTooltipActivityUtc = DateTime.MinValue;
    private DateTime _lastTooltipBlockedUtc = DateTime.MinValue;
    private bool _lastHasBandwidth;

    public void UpdateActivity(ProcessTrafficTracker tracker)
    {
        var activity = tracker.GetActivity(AppPath, Verdict);
        ActivityLevel = activity.Level;
        DownloadBps = activity.DownloadBps;
        UploadBps = activity.UploadBps;
        BandwidthSpeedText = activity.FormattedSpeed;
        HasActiveBandwidth = activity.HasBandwidth;

        if (!HasCountry)
        {
            var liveEp = activity.LastEndpoint ?? activity.LastBlockedEndpoint;
            if (!string.IsNullOrEmpty(liveEp))
            {
                var liveIp = NetworkEndpoint.ExtractAddress(liveEp);
                var liveGeo = GeoCountry.Lookup(liveIp);
                if (liveGeo.HasCountry)
                {
                    UpdateGeo(liveGeo);
                }
            }
        }

        bool needsTooltipUpdate = string.IsNullOrEmpty(_activityTooltip)
            || _lastTooltipLevel != activity.Level
            || _lastHasBandwidth != activity.HasBandwidth
            || _lastActiveSockets != activity.ActiveSockets
            || (activity.Level != ProcessActivityLevel.Idle && (_lastTooltipActivityUtc != activity.LastActivityUtc || _lastTooltipBlockedUtc != activity.LastBlockedUtc));

        if (needsTooltipUpdate)
        {
            _lastTooltipLevel = activity.Level;
            _lastHasBandwidth = activity.HasBandwidth;
            _lastActiveSockets = activity.ActiveSockets;
            _lastTooltipActivityUtc = activity.LastActivityUtc;
            _lastTooltipBlockedUtc = activity.LastBlockedUtc;
            ActivityTooltip = tracker.FormatTooltip(AppPath, Verdict);
        }
    }

    public void InvalidateTooltip()
    {
        _activityTooltip = string.Empty;
    }

    public void RefreshIcon()
    {
        OnPropertyChanged(nameof(AppPath));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

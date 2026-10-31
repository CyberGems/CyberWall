using CyberWall.Common.Geo;
using Xunit;

namespace CyberWall.Tests;

public sealed class GeoCountryTests
{
    [Fact]
    public void Lookup_WellKnownAnycast_ReturnsExpectedCountry()
    {
        var resGoogle = GeoCountry.Lookup("8.8.8.8");
        Assert.True(resGoogle.HasCountry);
        Assert.Equal("US", resGoogle.Iso2);

        var resCloudflare = GeoCountry.Lookup("1.1.1.1:443");
        Assert.True(resCloudflare.HasCountry);
        Assert.Equal("US", resCloudflare.Iso2);

        var resQuad9 = GeoCountry.Lookup("9.9.9.9:53");
        Assert.True(resQuad9.HasCountry);
        Assert.Equal("US", resQuad9.Iso2);
    }

    [Fact]
    public void Lookup_LocalAndPrivateIPs_ReturnsLocal()
    {
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("127.0.0.1").Kind);
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("192.168.1.1").Kind);
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("10.0.0.5:8080").Kind);
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("172.16.0.1").Kind);
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("0.0.0.0").Kind);
        Assert.Equal(GeoKind.Local, GeoCountry.Lookup("::1").Kind);
    }

    [Fact]
    public void Lookup_InvalidOrEmpty_ReturnsUnknown()
    {
        Assert.Equal(GeoKind.Unknown, GeoCountry.Lookup("").Kind);
        Assert.Equal(GeoKind.Unknown, GeoCountry.Lookup(null).Kind);
        Assert.Equal(GeoKind.Unknown, GeoCountry.Lookup("invalid_ip").Kind);
    }
}


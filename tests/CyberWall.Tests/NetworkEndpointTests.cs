using CyberWall.Common;

namespace CyberWall.Tests;

public class NetworkEndpointTests
{
    [Theory]
    [InlineData(443, "HTTPS")]
    [InlineData(53, "DNS")]
    [InlineData(445, "SMB")]
    [InlineData(22, "SSH")]
    public void KnownPortsGetTheirServiceName(int port, string expected)
    {
        Assert.Equal(expected, NetworkEndpoint.ServiceLabel("TCP", port));
    }

    [Fact]
    public void UnknownPortsFallBackToTheProtocol()
    {
        Assert.Equal("UDP", NetworkEndpoint.ServiceLabel("UDP", 51234));
        Assert.Equal("TCP", NetworkEndpoint.ServiceLabel("", 51234));
    }

    [Fact]
    public void FormatPrimaryUsesTheHostNameWhenAvailable()
    {
        var result = NetworkEndpoint.FormatPrimary("TCP", "93.184.216.34", 443, "example.com");
        Assert.Equal("HTTPS · example.com:443", result);
    }

    [Fact]
    public void FormatPrimaryFallsBackToTheAddress()
    {
        var result = NetworkEndpoint.FormatPrimary("TCP", "93.184.216.34", 443);
        Assert.Equal("HTTPS · 93.184.216.34:443", result);
    }

    [Fact]
    public void FormatSecondaryAppendsTheProcessIdWhenAHostIsKnown()
    {
        Assert.Equal("93.184.216.34 · PID 1234", NetworkEndpoint.FormatSecondary("93.184.216.34", 1234, hasHost: true));
    }

    [Fact]
    public void FormatSecondaryOnlyShowsTheProcessIdWithoutAHost()
    {
        Assert.Equal("PID 1234", NetworkEndpoint.FormatSecondary("93.184.216.34", 1234, hasHost: false));
    }

    [Theory]
    [InlineData("93.184.216.34:443", "93.184.216.34")]
    [InlineData("[2606:2800:220:1:248:1893:25c8:1946]:443", "2606:2800:220:1:248:1893:25c8:1946")]
    [InlineData("93.184.216.34", "93.184.216.34")]
    public void ExtractAddressReadsTheAddressOfAnEndpoint(string endpoint, string expected)
    {
        Assert.Equal(expected, NetworkEndpoint.ExtractAddress(endpoint));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("host:not-an-address")]
    public void ExtractAddressRejectsInvalidEndpoints(string? endpoint)
    {
        Assert.Null(NetworkEndpoint.ExtractAddress(endpoint));
    }
}

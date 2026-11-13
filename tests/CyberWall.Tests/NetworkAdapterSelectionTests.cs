using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class NetworkAdapterSelectionTests
{
    private sealed class FakeIpProps : IPInterfaceProperties
    {
        private readonly UnicastIPAddressInformationCollection _unicast = new FakeUnicastCollection();

        public FakeIpProps(bool withUnicast)
        {
            if (withUnicast)
            {
                ((FakeUnicastCollection)_unicast).AddAddress(new FakeUnicastAddress(IPAddress.Parse("192.168.1.50")));
            }
        }

        public override UnicastIPAddressInformationCollection UnicastAddresses => _unicast;
        public override IPAddressInformationCollection AnycastAddresses => throw new NotSupportedException();
        public override IPAddressCollection DhcpServerAddresses => throw new NotSupportedException();
        public override IPAddressCollection DnsAddresses => throw new NotSupportedException();
        public override string DnsSuffix => string.Empty;
        public override GatewayIPAddressInformationCollection GatewayAddresses => new FakeGatewayCollection();
        public override bool IsDnsEnabled => false;
        public override bool IsDynamicDnsEnabled => false;
        public override MulticastIPAddressInformationCollection MulticastAddresses => throw new NotSupportedException();
        public override IPAddressCollection WinsServersAddresses => throw new NotSupportedException();
        public override IPv4InterfaceProperties GetIPv4Properties() => throw new NotSupportedException();
        public override IPv6InterfaceProperties GetIPv6Properties() => throw new NotSupportedException();
    }

    private sealed class FakeGatewayCollection : GatewayIPAddressInformationCollection { }

    private sealed class FakeUnicastCollection : UnicastIPAddressInformationCollection
    {
        private readonly List<UnicastIPAddressInformation> _items = new();
        public void AddAddress(UnicastIPAddressInformation item) => _items.Add(item);
        public override int Count => _items.Count;
        public override UnicastIPAddressInformation this[int index] => _items[index];
        public override IEnumerator<UnicastIPAddressInformation> GetEnumerator() => _items.GetEnumerator();
    }

    private sealed class FakeUnicastAddress : UnicastIPAddressInformation
    {
        private readonly IPAddress _address;
        public FakeUnicastAddress(IPAddress address) => _address = address;
        public override IPAddress Address => _address;
        public override bool IsDnsEligible => true;
        public override bool IsTransient => false;
        public override long AddressPreferredLifetime => 0;
        public override long AddressValidLifetime => 0;
        public override long DhcpLeaseLifetime => 0;
        public override DuplicateAddressDetectionState DuplicateAddressDetectionState => DuplicateAddressDetectionState.Preferred;
        public override IPAddress IPv4Mask => IPAddress.Parse("255.255.255.0");
        public override PrefixOrigin PrefixOrigin => PrefixOrigin.Dhcp;
        public override SuffixOrigin SuffixOrigin => SuffixOrigin.OriginDhcp;
    }

    private sealed class FakeNic : NetworkInterface
    {
        private readonly IPInterfaceProperties _ipProps;

        public FakeNic(
            string id,
            string name,
            string description,
            OperationalStatus status,
            NetworkInterfaceType type,
            bool hasUnicastIp = true)
        {
            Id = id;
            Name = name;
            Description = description;
            OperationalStatus = status;
            NetworkInterfaceType = type;
            _ipProps = new FakeIpProps(hasUnicastIp);
        }

        public override string Id { get; }
        public override string Name { get; }
        public override string Description { get; }
        public override OperationalStatus OperationalStatus { get; }
        public override NetworkInterfaceType NetworkInterfaceType { get; }
        public override IPInterfaceProperties GetIPProperties() => _ipProps;
    }

    [Fact]
    public void PreferredNetworkAdapter_DefaultsEmptyAndRestoresViaPreferences()
    {
        var defaults = new AppSettings();
        Assert.Equal("", defaults.PreferredNetworkAdapterId);
        Assert.Equal("", defaults.PreferredNetworkAdapterName);

        var saved = new AppSettings
        {
            PreferredNetworkAdapterId = "{ADAPTER-GUID-123}",
            PreferredNetworkAdapterName = "Realtek PCIe GbE"
        };
        var json = JsonSerializer.Serialize(saved);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;

        var live = new AppSettings();
        live.ApplyRestoredPreferences(deserialized);

        Assert.Equal("{ADAPTER-GUID-123}", live.PreferredNetworkAdapterId);
        Assert.Equal("Realtek PCIe GbE", live.PreferredNetworkAdapterName);
    }

    [Fact]
    public void BuildSelectableAdapters_FiltersSystemNoiseAndOrdersPhysicalUpFirst()
    {
        var nics = new NetworkInterface[]
        {
            new FakeNic("loop", "Loopback", "Software Loopback Interface 1", OperationalStatus.Up, NetworkInterfaceType.Loopback),
            new FakeNic("lwf", "Ethernet-WFP", "Realtek-WFP Native MAC Layer LightWeight Filter-0000", OperationalStatus.Up, NetworkInterfaceType.Ethernet),
            new FakeNic("wan", "WAN", "WAN Miniport (IP)", OperationalStatus.Down, NetworkInterfaceType.Ppp, hasUnicastIp: false),
            new FakeNic("vpn", "WireGuard", "WireGuard Tunnel", OperationalStatus.Up, NetworkInterfaceType.Tunnel),
            new FakeNic("eth-down", "Ethernet", "Intel Ethernet Controller", OperationalStatus.Down, NetworkInterfaceType.Ethernet, hasUnicastIp: false),
            new FakeNic("wifi-up", "Wi-Fi", "Intel Wi-Fi 6 AX200", OperationalStatus.Up, NetworkInterfaceType.Wireless80211)
        };

        var selectable = NetworkSpeedService.BuildSelectableAdapters(nics);

        Assert.Equal(3, selectable.Count);
        Assert.Equal("wifi-up", selectable[0].Id);
        Assert.True(selectable[0].IsUp);
        Assert.False(selectable[0].IsVirtualOrVpn);

        Assert.Equal("vpn", selectable[1].Id);
        Assert.True(selectable[1].IsUp);
        Assert.True(selectable[1].IsVirtualOrVpn);

        Assert.Equal("eth-down", selectable[2].Id);
        Assert.False(selectable[2].IsUp);
    }

    [Fact]
    public void SelectTargetAdapter_PinsChosenAdapterAndDoesNotFallbackWhenDisconnected()
    {
        var ethDown = new FakeNic("eth", "Ethernet", "Intel Ethernet", OperationalStatus.Down, NetworkInterfaceType.Ethernet, hasUnicastIp: false);
        var wifiUp = new FakeNic("wifi", "Wi-Fi", "Intel Wi-Fi 6", OperationalStatus.Up, NetworkInterfaceType.Wireless80211);
        var vpnUp = new FakeNic("vpn", "wg0", "WireGuard Tunnel", OperationalStatus.Up, NetworkInterfaceType.Tunnel);
        var nics = new NetworkInterface[] { ethDown, wifiUp, vpnUp };

        Assert.Same(vpnUp, NetworkSpeedService.SelectTargetAdapter(nics, "vpn"));
        Assert.Same(wifiUp, NetworkSpeedService.SelectTargetAdapter(nics, "WIFI"));
        Assert.Null(NetworkSpeedService.SelectTargetAdapter(nics, "eth"));
        Assert.Null(NetworkSpeedService.SelectTargetAdapter(nics, "missing-adapter"));
    }

    [Fact]
    public void SetPreferredAdapter_TracksIdCleanly()
    {
        var svc = NetworkSpeedService.Instance;
        var previous = svc.PreferredAdapterId;
        try
        {
            svc.SetPreferredAdapter("{NON-EXISTENT-ADAPTER-ID}", "USB Ethernet");
            Assert.Equal("{NON-EXISTENT-ADAPTER-ID}", svc.PreferredAdapterId);
            Assert.False(svc.CurrentSnapshot.IsConnected);
            Assert.Contains("USB Ethernet", svc.CurrentSnapshot.AdapterName);

            svc.SetPreferredAdapter("");
            Assert.Equal("", svc.PreferredAdapterId);
        }
        finally
        {
            svc.SetPreferredAdapter(previous);
        }
    }
}


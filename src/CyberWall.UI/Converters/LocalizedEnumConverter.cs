using System.Globalization;
using System.Windows.Data;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;

namespace CyberWall.UI.Converters;

public sealed class VerdictDisplayConverter : IValueConverter
{
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is Verdict vd ? (vd == Verdict.Allow ? Strings.T("Allow") : Strings.T("Block")) : v?.ToString() ?? "";

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class DirectionDisplayConverter : IValueConverter
{
    public object Convert(object v, Type _, object __, CultureInfo ___)
    {
        if (v is AppRuleRow row)
        {
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Allow)
                return $"⇄ {Strings.T("Both")}";
            if (row.InboundVerdict == Verdict.Block && row.OutboundVerdict == Verdict.Allow)
                return $"↑ {Strings.T("DirectionOutboundOnly")}";
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Block)
                return $"↓ {Strings.T("DirectionInboundOnly")}";
            return $"⛔ {Strings.T("DirectionNone")}";
        }
        if (v is AppRule rule)
        {
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return $"⇄ {Strings.T("Both")}";
            if (rule.EffectiveInboundVerdict == Verdict.Block && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return $"↑ {Strings.T("DirectionOutboundOnly")}";
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Block)
                return $"↓ {Strings.T("DirectionInboundOnly")}";
            return $"⛔ {Strings.T("DirectionNone")}";
        }
        if (v is Direction d)
        {
            return d switch
            {
                Direction.Inbound => $"↓ {Strings.T("Inbound")}",
                Direction.Outbound => $"↑ {Strings.T("Outbound")}",
                _ => $"⇄ {Strings.T("Both")}"
            };
        }
        return v?.ToString() ?? "";
    }

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}


public sealed class DirectionPathConverter : IValueConverter
{
    private const string PathBoth = "M 0 1.5 H 6.5 V 0 L 11 2.75 L 6.5 5.5 V 4 H 0 Z M 11 7.5 H 4.5 V 6 L 0 8.75 L 4.5 11.5 V 10 H 11 Z";
    private const string PathOutbound = "M 5.5 0 L 10.5 4.5 H 7.5 V 11 H 3.5 V 4.5 H 0.5 Z";
    private const string PathInbound = "M 3.5 0 H 7.5 V 6.5 H 10.5 L 5.5 11 L 0.5 6.5 H 3.5 Z";
    private const string PathNone = "M 1 4.5 H 10 V 6.5 H 1 Z M 5.5 0.5 A 5 5 0 1 0 5.5 10.5 A 5 5 0 1 0 5.5 0.5 M 5.5 2 A 3.5 3.5 0 1 1 5.5 9 A 3.5 3.5 0 1 1 5.5 2";

    public object Convert(object v, Type _, object __, CultureInfo ___)
    {
        if (v is AppRuleRow row)
        {
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Allow)
                return PathBoth;
            if (row.InboundVerdict == Verdict.Block && row.OutboundVerdict == Verdict.Allow)
                return PathOutbound;
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Block)
                return PathInbound;
            return PathNone;
        }
        if (v is AppRule rule)
        {
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return PathBoth;
            if (rule.EffectiveInboundVerdict == Verdict.Block && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return PathOutbound;
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Block)
                return PathInbound;
            return PathNone;
        }
        if (v is Direction d)
        {
            return d switch
            {
                Direction.Inbound => PathInbound,
                Direction.Outbound => PathOutbound,
                _ => PathBoth
            };
        }
        return PathBoth;
    }

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class DirectionTextConverter : IValueConverter
{
    public object Convert(object v, Type _, object __, CultureInfo ___)
    {
        if (v is AppRuleRow row)
        {
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Allow)
                return Strings.T("Both");
            if (row.InboundVerdict == Verdict.Block && row.OutboundVerdict == Verdict.Allow)
                return Strings.T("DirectionOutboundOnly");
            if (row.InboundVerdict == Verdict.Allow && row.OutboundVerdict == Verdict.Block)
                return Strings.T("DirectionInboundOnly");
            return Strings.T("DirectionNone");
        }
        if (v is AppRule rule)
        {
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return Strings.T("Both");
            if (rule.EffectiveInboundVerdict == Verdict.Block && rule.EffectiveOutboundVerdict == Verdict.Allow)
                return Strings.T("DirectionOutboundOnly");
            if (rule.EffectiveInboundVerdict == Verdict.Allow && rule.EffectiveOutboundVerdict == Verdict.Block)
                return Strings.T("DirectionInboundOnly");
            return Strings.T("DirectionNone");
        }
        if (v is Direction d)
        {
            return d switch
            {
                Direction.Inbound => Strings.T("Inbound"),
                Direction.Outbound => Strings.T("Outbound"),
                _ => Strings.T("Both")
            };
        }
        return v?.ToString() ?? "";
    }

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

public sealed class TranslationKeyConverter : IValueConverter
{
    public object Convert(object v, Type _, object p, CultureInfo ___) =>
        p is string key ? Strings.T(key) : (v is string s ? Strings.T(s) : "");

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

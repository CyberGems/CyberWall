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
    private const string PathBoth = "M 1 3.5 H 8.5 M 6 1 L 8.5 3.5 L 6 6 M 8 7.5 H 0.5 M 3 5 L 0.5 7.5 L 3 10";
    private const string PathOutbound = "M 4.5 9.5 V 1.5 M 1.5 4.5 L 4.5 1.5 L 7.5 4.5";
    private const string PathInbound = "M 4.5 1.5 V 9.5 M 1.5 6.5 L 4.5 9.5 L 7.5 6.5";
    private const string PathNone = "M 2 8 L 8 2 M 5 1 A 4 4 0 1 0 5 9 A 4 4 0 1 0 5 1";

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

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

public sealed class TranslationKeyConverter : IValueConverter
{
    public object Convert(object v, Type _, object p, CultureInfo ___) =>
        p is string key ? Strings.T(key) : (v is string s ? Strings.T(s) : "");

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

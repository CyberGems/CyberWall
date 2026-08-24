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
    public object Convert(object v, Type _, object __, CultureInfo ___) =>
        v is Direction d ? (d == Direction.Inbound ? $"↓ {Strings.T("Inbound")}" : d == Direction.Outbound ? $"↑ {Strings.T("Outbound")}" : $"⇄ {Strings.T("Both")}") : v?.ToString() ?? "";

    public object ConvertBack(object v, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

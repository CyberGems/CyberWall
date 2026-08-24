using System.Globalization;
using System.Windows.Data;
using CyberWall.Common.Models;

namespace CyberWall.UI.Converters;

public sealed class VerdictToToggleConverter : IValueConverter
{
    public object Convert(object value, Type _, object __, CultureInfo ___) => value is Verdict v && v == Verdict.Allow;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) => value is bool b && b ? Verdict.Allow : Verdict.Block;
}

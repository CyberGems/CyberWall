using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;

namespace CyberWall.UI.Converters;

public sealed class PathToIconConverter : IValueConverter
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Lock = new();

    public object? Convert(object value, Type _, object __, CultureInfo ___)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        lock (Lock)
        {
            if (Cache.TryGetValue(path, out var c)) return c;
            try
            {
                if (!File.Exists(path)) { Cache[path] = null; return null; }
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null) { Cache[path] = null; return null; }
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = ms;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                Cache[path] = img;
                return img;
            }
            catch { Cache[path] = null; return null; }
        }
    }

    public object ConvertBack(object value, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

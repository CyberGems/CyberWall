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
        }

        var img = LoadIcon(path);
        lock (Lock)
        {
            Cache[path] = img;
        }
        return img;
    }

    public static void Prewarm(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            lock (Lock)
            {
                if (Cache.ContainsKey(path)) continue;
            }

            var img = LoadIcon(path);
            lock (Lock)
            {
                Cache[path] = img;
            }
        }
    }

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
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
            return img;
        }
        catch { return null; }
    }

    public object ConvertBack(object value, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

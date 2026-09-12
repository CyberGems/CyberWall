using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CyberWall.UI.Converters;

public sealed class PathToIconConverter : IValueConverter
{
    private static readonly Dictionary<string, (ImageSource? Image, string? Hash)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Lock = new();

    public static event Action<string>? IconChanged;

    public object? Convert(object value, Type _, object __, CultureInfo ___)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        lock (Lock)
        {
            if (Cache.TryGetValue(path, out var c)) return c.Image;
        }

        var (img, hash) = ExtractIconWithHash(path);
        lock (Lock)
        {
            Cache[path] = (img, hash);
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

            var (img, hash) = ExtractIconWithHash(path);
            lock (Lock)
            {
                Cache[path] = (img, hash);
            }
        }
    }

    public static string? GetCachedHash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        lock (Lock)
        {
            return Cache.TryGetValue(path, out var entry) ? entry.Hash : null;
        }
    }

    public static void UpdateCache(string path, ImageSource? image, string? hash, bool notify = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        lock (Lock)
        {
            Cache[path] = (image, hash);
        }

        if (notify)
        {
            IconChanged?.Invoke(path);
        }
    }

    public static (ImageSource? Image, string? Hash) ExtractIconWithHash(string path)
    {
        try
        {
            if (!File.Exists(path)) return (null, null);
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return (null, null);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = ms.ToArray();

            var hashBytes = SHA256.HashData(bytes);
            var hashStr = System.Convert.ToHexString(hashBytes);

            ms.Position = 0;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();

            return (img, hashStr);
        }
        catch
        {
            return (null, null);
        }
    }

    public object ConvertBack(object value, Type _, object __, CultureInfo ___) => throw new NotSupportedException();
}

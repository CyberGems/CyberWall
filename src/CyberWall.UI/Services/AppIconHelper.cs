using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using WpfImageSource = System.Windows.Media.ImageSource;

namespace CyberWall.UI.Services;

public static class AppIconHelper
{
    private static System.Drawing.Icon? _trayIcon;
    private static System.Drawing.Icon? _trayIconMuted;
    private static WpfImageSource? _cachedImageSource;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Tray icon. The muted variant is used while the protection is off, so the state
    /// of the firewall is visible at a glance in the notification area.
    /// </summary>
    public static System.Drawing.Icon CreateShieldIcon(int size = 32, bool muted = false)
    {
        if (muted)
        {
            if (_trayIconMuted != null) return _trayIconMuted;
            using var mutedBmp = GenerateShieldBitmap(size, muted: true);
            var mutedHandle = mutedBmp.GetHicon();
            var mutedIcon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(mutedHandle).Clone();
            DestroyIcon(mutedHandle);
            _trayIconMuted = mutedIcon;
            return mutedIcon;
        }

        if (_trayIcon != null) return _trayIcon;

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/CyberWall.ico", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri != null)
            {
                using var stream = sri.Stream;
                var icon = new System.Drawing.Icon(stream, size, size);
                _trayIcon = icon;
                return icon;
            }
        }
        catch { }

        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "CyberWall.ico");
            if (File.Exists(iconPath))
            {
                var icon = new System.Drawing.Icon(iconPath, size, size);
                _trayIcon = icon;
                return icon;
            }
        }
        catch { }

        using var bmp = GenerateShieldBitmap(size);
        var hIcon = bmp.GetHicon();
        var fallbackIcon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        _trayIcon = fallbackIcon;
        return fallbackIcon;
    }

    public static WpfImageSource CreateShieldImageSource(int size = 256)
    {
        if (_cachedImageSource != null) return _cachedImageSource;

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/CyberWall.ico", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri != null)
            {
                using var stream = sri.Stream;
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var bestFrame = decoder.Frames.OrderByDescending(f => f.Width).FirstOrDefault();
                if (bestFrame != null)
                {
                    _cachedImageSource = bestFrame;
                    return bestFrame;
                }
            }
        }
        catch { }

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/CyberWall.png", UriKind.Absolute);
            var bi = BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            _cachedImageSource = bi;
            return bi;
        }
        catch { }

        using var bmp = GenerateShieldBitmap(size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var fallback = new BitmapImage();
        fallback.BeginInit();
        fallback.StreamSource = ms;
        fallback.CacheOption = BitmapCacheOption.OnLoad;
        fallback.EndInit();
        fallback.Freeze();

        _cachedImageSource = fallback;
        return fallback;
    }

    private static Bitmap GenerateShieldBitmap(int size, bool muted = false)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Draw rounded badge container
        float r = size * 0.22f;
        using var badgePath = GetRoundedRectPath(new RectangleF(0.5f, 0.5f, size - 1f, size - 1f), r);
        using var bgBrush = muted
            ? new SolidBrush(Color.FromArgb(255, 28, 32, 40)) // #1C2028
            : new SolidBrush(Color.FromArgb(255, 11, 19, 34)); // #0B1322
        using var borderPen = muted
            ? new Pen(Color.FromArgb(255, 60, 66, 78), 1.2f)
            : new Pen(Color.FromArgb(255, 30, 48, 80), 1.2f); // #1E3050
        g.FillPath(bgBrush, badgePath);
        g.DrawPath(borderPen, badgePath);

        // Draw shield
        using var shieldPath = new GraphicsPath();
        float cx = size / 2f;
        float cy = size / 2f;
        float sw = size * 0.52f;
        float sh = size * 0.58f;

        var pTop = new PointF(cx, cy - sh * 0.48f);
        var pLeft = new PointF(cx - sw * 0.48f, cy - sh * 0.32f);
        var pLeftMid = new PointF(cx - sw * 0.48f, cy + sh * 0.05f);
        var pBottom = new PointF(cx, cy + sh * 0.48f);
        var pRightMid = new PointF(cx + sw * 0.48f, cy + sh * 0.05f);
        var pRight = new PointF(cx + sw * 0.48f, cy - sh * 0.32f);

        shieldPath.AddLine(pTop, pLeft);
        shieldPath.AddLine(pLeft, pLeftMid);
        shieldPath.AddBezier(pLeftMid, new PointF(cx - sw * 0.45f, cy + sh * 0.32f), new PointF(cx - sw * 0.2f, cy + sh * 0.45f), pBottom);
        shieldPath.AddBezier(pBottom, new PointF(cx + sw * 0.2f, cy + sh * 0.45f), new PointF(cx + sw * 0.45f, cy + sh * 0.32f), pRightMid);
        shieldPath.AddLine(pRightMid, pRight);
        shieldPath.CloseFigure();

        // Fill shield with vibrant cyan gradient (#00F0FF -> #00B4D8)
        using var shieldBrush = muted
            ? new LinearGradientBrush(
                new PointF(cx, cy - sh / 2f),
                new PointF(cx, cy + sh / 2f),
                Color.FromArgb(255, 120, 128, 140),
                Color.FromArgb(255, 88, 94, 104))
            : new LinearGradientBrush(
                new PointF(cx, cy - sh / 2f),
                new PointF(cx, cy + sh / 2f),
                Color.FromArgb(255, 0, 240, 255),
                Color.FromArgb(255, 0, 160, 220));
        g.FillPath(shieldBrush, shieldPath);

        return bmp;
    }

    private static GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

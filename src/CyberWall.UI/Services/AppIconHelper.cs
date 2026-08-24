using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
    private static WpfImageSource? _cachedImageSource;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static System.Drawing.Icon CreateShieldIcon(int size = 32)
    {
        if (_trayIcon != null) return _trayIcon;

        using var bmp = GenerateShieldBitmap(size);
        var hIcon = bmp.GetHicon();
        var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        _trayIcon = icon;
        return icon;
    }

    public static WpfImageSource CreateShieldImageSource(int size = 64)
    {
        if (_cachedImageSource != null) return _cachedImageSource;

        using var bmp = GenerateShieldBitmap(size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = ms;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();

        _cachedImageSource = bi;
        return bi;
    }

    private static Bitmap GenerateShieldBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Draw rounded badge container
        float r = size * 0.22f;
        using var badgePath = GetRoundedRectPath(new RectangleF(0.5f, 0.5f, size - 1f, size - 1f), r);
        using var bgBrush = new SolidBrush(Color.FromArgb(255, 11, 19, 34)); // #0B1322
        using var borderPen = new Pen(Color.FromArgb(255, 30, 48, 80), 1.2f); // #1E3050
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
        using var shieldBrush = new LinearGradientBrush(
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

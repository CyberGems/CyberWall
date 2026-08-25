using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace CyberWall.UI.Controls;

internal static class FlagPainter
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private const double W = 60;
    private const double H = 40;

    public static ImageSource? Create(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso) || iso.Trim().Length != 2) return null;
        iso = iso.Trim().ToUpperInvariant();
        if (iso[0] is < 'A' or > 'Z' || iso[1] is < 'A' or > 'Z') return null;
        lock (Cache)
        {
            if (Cache.TryGetValue(iso, out var hit)) return hit;
            var img = new DrawingImage(Draw(iso));
            img.Freeze();
            Cache[iso] = img;
            return img;
        }
    }

    private static Drawing Draw(string iso)
    {
        var dg = new DrawingGroup();
        using (var ctx = dg.Open())
        {
            var clip = new RectangleGeometry(new Rect(0, 0, W, H), 3, 3);
            ctx.PushClip(clip);
            Paint(ctx, iso);
            ctx.Pop();
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 1), clip);
        }
        dg.Freeze();
        return dg;
    }

    private static void Paint(DrawingContext dc, string iso)
    {
        switch (iso)
        {
            case "US": DrawUs(dc); return;
            case "GB":
            case "UK": DrawGb(dc); return;
            case "JP": DrawJp(dc); return;
            case "CH": DrawCh(dc); return;
            case "CN": DrawCn(dc); return;
            case "KR": DrawKr(dc); return;
            case "TR": DrawTr(dc); return;
            case "BR": DrawBr(dc); return;
            case "IN": DrawIn(dc); return;
            case "ZA": DrawZa(dc); return;
            case "GR": DrawGr(dc); return;
            case "AU":
            case "NZ": DrawAu(dc); return;
            case "IL": DrawIl(dc); return;
            case "AE": DrawBandedWithHoist(dc, ["#00732F", "#FFFFFF", "#000000"], "#FF0000"); return;
            case "KW": DrawKw(dc); return;
            case "QA":
                dc.DrawRectangle(Br("#8A1538"), null, new Rect(0, 0, W, H));
                dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W * 0.28, H));
                return;
            case "SA":
                dc.DrawRectangle(Br("#006C35"), null, new Rect(0, 0, W, H));
                return;
            case "PK":
                dc.DrawRectangle(Br("#01411C"), null, new Rect(0, 0, W, H));
                dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W * 0.22, H));
                dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.62, H / 2), H * 0.18, H * 0.18);
                return;
            case "BD":
                dc.DrawRectangle(Br("#006A4E"), null, new Rect(0, 0, W, H));
                dc.DrawEllipse(Br("#F42A41"), null, new Point(W * 0.42, H / 2), H * 0.28, H * 0.28);
                return;
            case "VN":
                dc.DrawRectangle(Br("#DA251D"), null, new Rect(0, 0, W, H));
                Star(dc, W / 2, H / 2, H * 0.22, Br("#FFDE00"));
                return;
            case "TW": DrawTw(dc); return;
            case "HK":
                dc.DrawRectangle(Br("#DE2910"), null, new Rect(0, 0, W, H));
                return;
            case "EU": DrawEu(dc); return;
            case "ES": DrawEs(dc); return;
            case "PT": DrawPt(dc); return;
            case "CL": DrawCl(dc); return;
            case "UY": DrawUy(dc); return;
            case "CU":
            case "PR": DrawCu(dc); return;
            case "PH": DrawPh(dc); return;
            case "MY": DrawMy(dc); return;
            case "SG": DrawSg(dc); return;
            case "GE": DrawGe(dc); return;
            case "DK": DrawNordic(dc, "#C8102E", "#FFFFFF", null); return;
            case "FI": DrawNordic(dc, "#FFFFFF", "#003580", null); return;
            case "SE": DrawNordic(dc, "#006AA7", "#FECC00", null); return;
            case "NO": DrawNordic(dc, "#BA0C2F", "#FFFFFF", "#00205B"); return;
            case "IS": DrawNordic(dc, "#02529C", "#FFFFFF", "#DC1E35"); return;
            case "FO": DrawNordic(dc, "#FFFFFF", "#0065BD", "#ED2939"); return;
        }

        if (Vertical.TryGetValue(iso, out var v))
        {
            Stripes(dc, v, vertical: true);
            return;
        }
        if (Horizontal.TryGetValue(iso, out var h))
        {
            Stripes(dc, h, vertical: false);
            return;
        }

        Stripes(dc, [Shade(iso, 0), Shade(iso, 1), Shade(iso, 2)], vertical: false);
    }

    private static readonly Dictionary<string, string[]> Horizontal = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AM"] = ["#D90012", "#0033A0", "#F2A800"],
        ["AR"] = ["#74ACDF", "#FFFFFF", "#74ACDF"],
        ["AT"] = ["#ED2939", "#FFFFFF", "#ED2939"],
        ["AZ"] = ["#00B5E2", "#EF3340", "#509E2F"],
        ["BG"] = ["#FFFFFF", "#00966E", "#D62612"],
        ["BO"] = ["#D52B1E", "#F9E300", "#007934"],
        ["CO"] = ["#FCD116", "#003893", "#CE1126"],
        ["CZ"] = ["#FFFFFF", "#D7141A"],
        ["DE"] = ["#000000", "#DD0000", "#FFCE00"],
        ["EC"] = ["#FFDD00", "#034EA2", "#ED1C24"],
        ["EE"] = ["#0072CE", "#000000", "#FFFFFF"],
        ["ET"] = ["#078930", "#FCDD09", "#DA121A"],
        ["GA"] = ["#009E60", "#FCD116", "#3A75C4"],
        ["GH"] = ["#CE1126", "#FCD116", "#006B3F"],
        ["HR"] = ["#FF0000", "#FFFFFF", "#171796"],
        ["HU"] = ["#CE2939", "#FFFFFF", "#477050"],
        ["ID"] = ["#CE1126", "#FFFFFF"],
        ["IQ"] = ["#CE1126", "#FFFFFF", "#000000"],
        ["IR"] = ["#239F40", "#FFFFFF", "#DA0000"],
        ["KE"] = ["#000000", "#BB0000", "#006600"],
        ["LT"] = ["#FDB913", "#006A44", "#C1272D"],
        ["LU"] = ["#ED2939", "#FFFFFF", "#00A1DE"],
        ["LV"] = ["#9E3039", "#FFFFFF", "#9E3039"],
        ["MC"] = ["#CE1126", "#FFFFFF"],
        ["NL"] = ["#AE1C28", "#FFFFFF", "#21468B"],
        ["PL"] = ["#FFFFFF", "#DC143C"],
        ["RU"] = ["#FFFFFF", "#0039A6", "#D52B1E"],
        ["SI"] = ["#FFFFFF", "#005DA4", "#ED1C24"],
        ["SK"] = ["#FFFFFF", "#0B4EA2", "#EE1C25"],
        ["SL"] = ["#1EB53A", "#FFFFFF", "#0072C6"],
        ["TH"] = ["#A51931", "#FFFFFF", "#2D2A4A", "#FFFFFF", "#A51931"],
        ["UA"] = ["#0057B8", "#FFD700"],
        ["UZ"] = ["#0099B5", "#FFFFFF", "#1EB53A"],
        ["VE"] = ["#FFCC00", "#00247D", "#CF142B"],
        ["YE"] = ["#CE1126", "#FFFFFF", "#000000"],
    };

    private static readonly Dictionary<string, string[]> Vertical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BE"] = ["#000000", "#FAE042", "#ED2939"],
        ["CA"] = ["#FF0000", "#FFFFFF", "#FF0000"],
        ["CI"] = ["#FF8200", "#FFFFFF", "#009A44"],
        ["FR"] = ["#002395", "#FFFFFF", "#ED2939"],
        ["GN"] = ["#CE1126", "#FCD116", "#009460"],
        ["GT"] = ["#4997D0", "#FFFFFF", "#4997D0"],
        ["HN"] = ["#0073CF", "#FFFFFF", "#0073CF"],
        ["IE"] = ["#169B62", "#FFFFFF", "#FF883E"],
        ["IT"] = ["#009246", "#FFFFFF", "#CE2B37"],
        ["ML"] = ["#14B53A", "#FCD116", "#CE1126"],
        ["MN"] = ["#DA2032", "#0066B3", "#DA2032"],
        ["MX"] = ["#006847", "#FFFFFF", "#CE1126"],
        ["NG"] = ["#008751", "#FFFFFF", "#008751"],
        ["NI"] = ["#0067C6", "#FFFFFF", "#0067C6"],
        ["PE"] = ["#D91023", "#FFFFFF", "#D91023"],
        ["RO"] = ["#002B7F", "#FCD116", "#CE1126"],
        ["SN"] = ["#00853F", "#FDEF42", "#E31B23"],
        ["SV"] = ["#0047AB", "#FFFFFF", "#0047AB"],
        ["TD"] = ["#002664", "#FECB00", "#C60C30"],
        ["CM"] = ["#007A5E", "#CE1126", "#FCD116"],
    };

    private static void Stripes(DrawingContext dc, string[] colors, bool vertical)
    {
        var n = colors.Length;
        for (int i = 0; i < n; i++)
        {
            var brush = Br(colors[i]);
            if (vertical)
                dc.DrawRectangle(brush, null, new Rect(i * W / n, 0, W / n + 0.5, H));
            else
                dc.DrawRectangle(brush, null, new Rect(0, i * H / n, W, H / n + 0.5));
        }
    }

    private static void DrawNordic(DrawingContext dc, string bg, string cross, string? inner)
    {
        dc.DrawRectangle(Br(bg), null, new Rect(0, 0, W, H));
        double x = W * 0.32;
        double t = H * 0.28;
        dc.DrawRectangle(Br(cross), null, new Rect(x - t / 2, 0, t, H));
        dc.DrawRectangle(Br(cross), null, new Rect(0, (H - t) / 2, W, t));
        if (inner == null) return;
        double t2 = t * 0.45;
        dc.DrawRectangle(Br(inner), null, new Rect(x - t2 / 2, 0, t2, H));
        dc.DrawRectangle(Br(inner), null, new Rect(0, (H - t2) / 2, W, t2));
    }

    private static void DrawBandedWithHoist(DrawingContext dc, string[] bands, string hoist)
    {
        Stripes(dc, bands, false);
        dc.DrawRectangle(Br(hoist), null, new Rect(0, 0, W * 0.28, H));
    }

    private static void DrawUs(DrawingContext dc)
    {
        for (int i = 0; i < 13; i++)
            dc.DrawRectangle(Br(i % 2 == 0 ? "#B22234" : "#FFFFFF"), null, new Rect(0, i * H / 13, W, H / 13 + 0.4));
        var canton = new Rect(0, 0, W * 0.40, H * 7 / 13.0);
        dc.DrawRectangle(Br("#3C3B6E"), null, canton);
        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 6; c++)
        {
            double x = 4 + c * (canton.Width - 6) / 5.2;
            double y = 3 + r * (canton.Height - 5) / 4.2;
            dc.DrawEllipse(Br("#FFFFFF"), null, new Point(x, y), 1.15, 1.15);
        }
    }

    private static void DrawGb(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#012169"), null, new Rect(0, 0, W, H));
        var white = new Pen(Br("#FFFFFF"), 8);
        var red = new Pen(Br("#C8102E"), 4);
        dc.DrawLine(white, new Point(0, 0), new Point(W, H));
        dc.DrawLine(white, new Point(W, 0), new Point(0, H));
        dc.DrawLine(red, new Point(0, 0), new Point(W, H));
        dc.DrawLine(red, new Point(W, 0), new Point(0, H));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect((W - 10) / 2, 0, 10, H));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, (H - 10) / 2, W, 10));
        dc.DrawRectangle(Br("#C8102E"), null, new Rect((W - 5) / 2, 0, 5, H));
        dc.DrawRectangle(Br("#C8102E"), null, new Rect(0, (H - 5) / 2, W, 5));
    }

    private static void DrawJp(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W, H));
        dc.DrawEllipse(Br("#BC002D"), null, new Point(W / 2, H / 2), H * 0.30, H * 0.30);
    }

    private static void DrawCh(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FF0000"), null, new Rect(0, 0, W, H));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(W * 0.38, H * 0.18, W * 0.24, H * 0.64));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(W * 0.18, H * 0.38, W * 0.64, H * 0.24));
    }

    private static void DrawCn(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#DE2910"), null, new Rect(0, 0, W, H));
        Star(dc, W * 0.16, H * 0.28, 5.2, Br("#FFDE00"));
        Star(dc, W * 0.32, H * 0.14, 2.2, Br("#FFDE00"));
        Star(dc, W * 0.38, H * 0.26, 2.2, Br("#FFDE00"));
        Star(dc, W * 0.38, H * 0.40, 2.2, Br("#FFDE00"));
        Star(dc, W * 0.32, H * 0.50, 2.2, Br("#FFDE00"));
    }

    private static void DrawKr(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W, H));
        dc.DrawEllipse(Br("#CD2E3A"), null, new Point(W / 2 - 3, H / 2), 8, 8);
        dc.DrawEllipse(Br("#0047A0"), null, new Point(W / 2 + 3, H / 2), 8, 8);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W / 2, H / 2), 3.2, 3.2);
    }

    private static void DrawTr(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#E30A17"), null, new Rect(0, 0, W, H));
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.40, H / 2), 9, 9);
        dc.DrawEllipse(Br("#E30A17"), null, new Point(W * 0.45, H / 2), 7.2, 7.2);
        Star(dc, W * 0.58, H / 2, 4.5, Br("#FFFFFF"));
    }

    private static void DrawBr(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#009C3B"), null, new Rect(0, 0, W, H));
        var diamond = new StreamGeometry();
        using (var g = diamond.Open())
        {
            g.BeginFigure(new Point(W / 2, 5), true, true);
            g.LineTo(new Point(W - 6, H / 2), true, false);
            g.LineTo(new Point(W / 2, H - 5), true, false);
            g.LineTo(new Point(6, H / 2), true, false);
        }
        dc.DrawGeometry(Br("#FFDF00"), null, diamond);
        dc.DrawEllipse(Br("#002776"), null, new Point(W / 2, H / 2), 7, 7);
    }

    private static void DrawIn(DrawingContext dc)
    {
        Stripes(dc, ["#FF9933", "#FFFFFF", "#138808"], false);
        dc.DrawEllipse(Br("#000080"), null, new Point(W / 2, H / 2), 4.6, 4.6);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W / 2, H / 2), 3.4, 3.4);
        dc.DrawEllipse(Br("#000080"), null, new Point(W / 2, H / 2), 1.3, 1.3);
    }

    private static void DrawZa(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#DE3831"), null, new Rect(0, 0, W, H / 2));
        dc.DrawRectangle(Br("#002395"), null, new Rect(0, H / 2, W, H / 2));
        dc.DrawRectangle(Br("#FFB612"), null, new Rect(W * 0.34, H * 0.38, W, H * 0.24));
        var y = new StreamGeometry();
        using (var g = y.Open())
        {
            g.BeginFigure(new Point(0, 0), true, true);
            g.LineTo(new Point(W * 0.38, H / 2), true, false);
            g.LineTo(new Point(0, H), true, false);
        }
        dc.DrawGeometry(Br("#007A4D"), null, y);
    }

    private static void DrawGr(DrawingContext dc)
    {
        for (int i = 0; i < 9; i++)
            dc.DrawRectangle(Br(i % 2 == 0 ? "#0D5EAF" : "#FFFFFF"), null, new Rect(0, i * H / 9, W, H / 9 + 0.3));
        dc.DrawRectangle(Br("#0D5EAF"), null, new Rect(0, 0, W * 0.38, H * 5 / 9.0));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(W * 0.152, 0, W * 0.076, H * 5 / 9.0));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, H * 2 / 9.0, W * 0.38, H / 9.0));
    }

    private static void DrawAu(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#012169"), null, new Rect(0, 0, W, H));
        dc.PushTransform(new ScaleTransform(0.5, 0.5));
        DrawGb(dc);
        dc.Pop();
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.72, H * 0.55), 1.6, 1.6);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.84, H * 0.32), 1.3, 1.3);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.88, H * 0.62), 1.3, 1.3);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.70, H * 0.78), 1.2, 1.2);
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.58, H * 0.48), 1.1, 1.1);
    }

    private static void DrawIl(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W, H));
        dc.DrawRectangle(Br("#0038B8"), null, new Rect(0, H * 0.18, W, H * 0.12));
        dc.DrawRectangle(Br("#0038B8"), null, new Rect(0, H * 0.70, W, H * 0.12));
        var pen = new Pen(Br("#0038B8"), 1.4);
        dc.DrawLine(pen, new Point(W / 2, H * 0.34), new Point(W / 2 - 7, H * 0.62));
        dc.DrawLine(pen, new Point(W / 2 - 7, H * 0.62), new Point(W / 2 + 7, H * 0.62));
        dc.DrawLine(pen, new Point(W / 2 + 7, H * 0.62), new Point(W / 2, H * 0.34));
        dc.DrawLine(pen, new Point(W / 2, H * 0.66), new Point(W / 2 - 7, H * 0.38));
        dc.DrawLine(pen, new Point(W / 2 - 7, H * 0.38), new Point(W / 2 + 7, H * 0.38));
        dc.DrawLine(pen, new Point(W / 2 + 7, H * 0.38), new Point(W / 2, H * 0.66));
    }

    private static void DrawKw(DrawingContext dc)
    {
        Stripes(dc, ["#007A3D", "#FFFFFF", "#CE1126"], false);
        var tri = new StreamGeometry();
        using (var g = tri.Open())
        {
            g.BeginFigure(new Point(0, 0), true, true);
            g.LineTo(new Point(W * 0.28, H / 2), true, false);
            g.LineTo(new Point(0, H), true, false);
        }
        dc.DrawGeometry(Br("#000000"), null, tri);
    }

    private static void DrawTw(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FE0000"), null, new Rect(0, 0, W, H));
        dc.DrawRectangle(Br("#000095"), null, new Rect(0, 0, W * 0.45, H * 0.55));
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.22, H * 0.27), 6, 6);
    }

    private static void DrawEu(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#003399"), null, new Rect(0, 0, W, H));
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI * 2 / 12 - Math.PI / 2;
            Star(dc, W / 2 + Math.Cos(a) * 11, H / 2 + Math.Sin(a) * 11, 2.1, Br("#FFCC00"));
        }
    }

    private static void DrawEs(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#AA151B"), null, new Rect(0, 0, W, H));
        dc.DrawRectangle(Br("#F1BF00"), null, new Rect(0, H * 0.25, W, H * 0.50));
    }

    private static void DrawPt(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#006600"), null, new Rect(0, 0, W * 0.40, H));
        dc.DrawRectangle(Br("#FF0000"), null, new Rect(W * 0.40, 0, W * 0.60, H));
        dc.DrawEllipse(Br("#FFD700"), null, new Point(W * 0.40, H / 2), 6, 6);
    }

    private static void DrawCl(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W, H / 2));
        dc.DrawRectangle(Br("#D52B1E"), null, new Rect(0, H / 2, W, H / 2));
        dc.DrawRectangle(Br("#0039A6"), null, new Rect(0, 0, W * 0.33, H / 2));
        Star(dc, W * 0.165, H * 0.25, 4.2, Br("#FFFFFF"));
    }

    private static void DrawUy(DrawingContext dc)
    {
        for (int i = 0; i < 9; i++)
            dc.DrawRectangle(Br(i % 2 == 0 ? "#FFFFFF" : "#0038A8"), null, new Rect(0, i * H / 9, W, H / 9 + 0.3));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W * 0.38, H * 5 / 9.0));
        dc.DrawEllipse(Br("#FCD116"), null, new Point(W * 0.19, H * 0.22), 5, 5);
    }

    private static void DrawCu(DrawingContext dc)
    {
        for (int i = 0; i < 5; i++)
            dc.DrawRectangle(Br(i % 2 == 0 ? "#002A8F" : "#FFFFFF"), null, new Rect(0, i * H / 5, W, H / 5 + 0.3));
        var tri = new StreamGeometry();
        using (var g = tri.Open())
        {
            g.BeginFigure(new Point(0, 0), true, true);
            g.LineTo(new Point(W * 0.42, H / 2), true, false);
            g.LineTo(new Point(0, H), true, false);
        }
        dc.DrawGeometry(Br("#CF142B"), null, tri);
        Star(dc, W * 0.14, H / 2, 4.5, Br("#FFFFFF"));
    }

    private static void DrawPh(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#0038A8"), null, new Rect(0, 0, W, H / 2));
        dc.DrawRectangle(Br("#CE1126"), null, new Rect(0, H / 2, W, H / 2));
        var tri = new StreamGeometry();
        using (var g = tri.Open())
        {
            g.BeginFigure(new Point(0, 0), true, true);
            g.LineTo(new Point(W * 0.42, H / 2), true, false);
            g.LineTo(new Point(0, H), true, false);
        }
        dc.DrawGeometry(Br("#FFFFFF"), null, tri);
        Star(dc, W * 0.14, H / 2, 4, Br("#FCD116"));
    }

    private static void DrawMy(DrawingContext dc)
    {
        for (int i = 0; i < 14; i++)
            dc.DrawRectangle(Br(i % 2 == 0 ? "#CC0000" : "#FFFFFF"), null, new Rect(0, i * H / 14, W, H / 14 + 0.3));
        dc.DrawRectangle(Br("#000066"), null, new Rect(0, 0, W * 0.45, H * 0.55));
        dc.DrawEllipse(Br("#FFCC00"), null, new Point(W * 0.22, H * 0.26), 6, 6);
    }

    private static void DrawSg(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#EF3340"), null, new Rect(0, 0, W, H / 2));
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, H / 2, W, H / 2));
        dc.DrawEllipse(Br("#FFFFFF"), null, new Point(W * 0.18, H * 0.25), 6, 6);
    }

    private static void DrawGe(DrawingContext dc)
    {
        dc.DrawRectangle(Br("#FFFFFF"), null, new Rect(0, 0, W, H));
        dc.DrawRectangle(Br("#FF0000"), null, new Rect((W - 7) / 2, 0, 7, H));
        dc.DrawRectangle(Br("#FF0000"), null, new Rect(0, (H - 7) / 2, W, 7));
    }

    private static void Star(DrawingContext dc, double cx, double cy, double r, Brush brush)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (int i = 0; i < 5; i++)
            {
                double a = i * 72 - 90;
                double x = cx + Math.Cos(a * Math.PI / 180) * r;
                double y = cy + Math.Sin(a * Math.PI / 180) * r;
                if (i == 0) ctx.BeginFigure(new Point(x, y), true, true);
                else ctx.LineTo(new Point(x, y), true, false);
                double a2 = a + 36;
                ctx.LineTo(new Point(cx + Math.Cos(a2 * Math.PI / 180) * r * 0.4, cy + Math.Sin(a2 * Math.PI / 180) * r * 0.4), true, false);
            }
        }
        dc.DrawGeometry(brush, null, g);
    }

    private static SolidColorBrush Br(string hex)
    {
        hex = hex.TrimStart('#');
        var c = Color.FromRgb(
            byte.Parse(hex[..2], NumberStyles.HexNumber),
            byte.Parse(hex[2..4], NumberStyles.HexNumber),
            byte.Parse(hex[4..6], NumberStyles.HexNumber));
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static string Shade(string iso, int band)
    {
        int h = (iso[0] * 37 + iso[1] * 17 + band * 53) % 360;
        var color = ColorFromHsv(h, 0.72, band == 1 ? 0.95 : 0.62);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color ColorFromHsv(int h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}

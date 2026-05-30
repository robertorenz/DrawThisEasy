using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawThisEasy.Models;
using DrawThisEasy.Services;

namespace DrawThisEasy.Controls;

/// Builds the visual for a single shape (geometry + label) inside a Canvas of size w x h.
public static class ShapeFactory
{
    public const double StrokeThickness = 1.6;

    /// Renders the shape body into the supplied panel. Returns a list of shapes whose Fill/Stroke should be updated when colors change.
    public static (UIElement Container, System.Windows.Shapes.Shape[] StyledParts) BuildBody(ShapeKind kind, double w, double h, Brush fill, Brush stroke, string? stencil = null, string? image = null, string? rtf = null, string? richFallback = null)
    {
        var canvas = new Canvas { Width = w, Height = h, IsHitTestVisible = true, Background = Brushes.Transparent };

        return kind switch
        {
            ShapeKind.Image         => BuildImage(canvas, w, h, image),
            ShapeKind.RichText      => BuildRichText(canvas, w, h, fill, stroke, rtf, richFallback),
            ShapeKind.ServiceTile   => BuildServiceTile(canvas, w, h, fill, stroke, stencil),
            ShapeKind.Rectangle     => BuildRect(canvas, w, h, fill, stroke, 0),
            ShapeKind.Rounded       => BuildRect(canvas, w, h, fill, stroke, 10),
            ShapeKind.Ellipse       => BuildEllipse(canvas, w, h, fill, stroke),
            ShapeKind.Diamond       => BuildPolygon(canvas, fill, stroke, new Point(w/2, 0), new Point(w, h/2), new Point(w/2, h), new Point(0, h/2)),
            ShapeKind.Hexagon       => BuildPolygon(canvas, fill, stroke, new Point(w*0.22, 0), new Point(w*0.78, 0), new Point(w, h/2), new Point(w*0.78, h), new Point(w*0.22, h), new Point(0, h/2)),
            ShapeKind.Parallelogram => BuildPolygon(canvas, fill, stroke, new Point(w*0.18, 0), new Point(w, 0), new Point(w*0.82, h), new Point(0, h)),
            ShapeKind.Cylinder      => BuildCylinder(canvas, w, h, fill, stroke),
            ShapeKind.Cloud         => BuildCloud(canvas, w, h, fill, stroke),
            ShapeKind.Server        => BuildServer(canvas, w, h, fill, stroke),
            ShapeKind.Person        => BuildPerson(canvas, w, h, fill, stroke),
            ShapeKind.Queue         => BuildQueue(canvas, w, h, fill, stroke),
            ShapeKind.Note          => BuildNote(canvas, w, h, fill, stroke),
            ShapeKind.Text          => BuildText(canvas, w, h),
            _ => BuildRect(canvas, w, h, fill, stroke, 0)
        };
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildRect(Canvas c, double w, double h, Brush fill, Brush stroke, double radius)
    {
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h,
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness,
            RadiusX = radius, RadiusY = radius
        };
        Canvas.SetLeft(rect, 0); Canvas.SetTop(rect, 0);
        c.Children.Add(rect);
        return (c, new[] { (System.Windows.Shapes.Shape)rect });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildEllipse(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        var e = new System.Windows.Shapes.Ellipse
        {
            Width = w, Height = h,
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness
        };
        Canvas.SetLeft(e, 0); Canvas.SetTop(e, 0);
        c.Children.Add(e);
        return (c, new System.Windows.Shapes.Shape[] { e });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildPolygon(Canvas c, Brush fill, Brush stroke, params Point[] points)
    {
        var poly = new Polygon
        {
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness, StrokeLineJoin = PenLineJoin.Round
        };
        foreach (var p in points) poly.Points.Add(p);
        c.Children.Add(poly);
        return (c, new System.Windows.Shapes.Shape[] { poly });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildCylinder(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        var rx = w / 2.0;
        var ry = Math.Min(h * 0.12, 14);

        // Body fill (rectangle between top and bottom curves)
        var body = new Path
        {
            Fill = fill,
            Data = Geometry.Parse(FormattableString.Invariant($"M 0,{ry} L 0,{h - ry} A {rx},{ry} 0 0 0 {w},{h - ry} L {w},{ry} Z"))
        };
        // Top ellipse fill
        var top = new System.Windows.Shapes.Ellipse
        {
            Width = w, Height = ry * 2, Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness
        };
        Canvas.SetLeft(top, 0); Canvas.SetTop(top, 0);
        // Outline: vertical sides + bottom curve
        var outline = new Path
        {
            Stroke = stroke, StrokeThickness = StrokeThickness, Fill = Brushes.Transparent,
            Data = Geometry.Parse(FormattableString.Invariant($"M 0,{ry} L 0,{h - ry} A {rx},{ry} 0 0 0 {w},{h - ry} L {w},{ry}"))
        };

        c.Children.Add(body);
        c.Children.Add(outline);
        c.Children.Add(top);

        return (c, new System.Windows.Shapes.Shape[] { body, outline, top });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildCloud(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        // Build cloud as an explicit PathGeometry so we never call Geometry.Parse on a string
        // that gets a Transform stamped on it (that combination has surprised us before).
        var sx = w / 100.0;
        var sy = h / 60.0;
        Point P(double x, double y) => new Point(x * sx, y * sy);

        var fig = new PathFigure { StartPoint = P(20, 55), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new BezierSegment(P(5, 55),   P(0, 40),  P(12, 32), true));
        fig.Segments.Add(new BezierSegment(P(5, 18),   P(22, 12), P(32, 20), true));
        fig.Segments.Add(new BezierSegment(P(35, 5),   P(60, 3),  P(68, 18), true));
        fig.Segments.Add(new BezierSegment(P(82, 10),  P(96, 22), P(88, 36), true));
        fig.Segments.Add(new BezierSegment(P(100, 42), P(92, 58), P(78, 55), true));
        fig.Segments.Add(new BezierSegment(P(70, 62),  P(30, 62), P(20, 55), true));

        var geom = new PathGeometry();
        geom.Figures.Add(fig);

        var path = new Path
        {
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = StrokeThickness,
            StrokeLineJoin = PenLineJoin.Round,
            Data = geom
        };
        c.Children.Add(path);
        return (c, new System.Windows.Shapes.Shape[] { path });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildServer(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        // Three stacked rounded rectangles with a status LED on each.
        var slots = 3;
        var gap = h * 0.06;
        var slotH = (h - gap * (slots - 1)) / slots;
        var styled = new System.Collections.Generic.List<System.Windows.Shapes.Shape>();
        for (int i = 0; i < slots; i++)
        {
            var y = i * (slotH + gap);
            var slot = new System.Windows.Shapes.Rectangle
            {
                Width = w, Height = slotH,
                Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness,
                RadiusX = 4, RadiusY = 4
            };
            Canvas.SetLeft(slot, 0); Canvas.SetTop(slot, y);
            c.Children.Add(slot);
            styled.Add(slot);

            // LED
            var ledColor = i switch { 0 => "#10B981", 1 => "#F59E0B", _ => "#0EA5E9" };
            var led = new System.Windows.Shapes.Ellipse
            {
                Width = Math.Min(slotH * 0.3, 8), Height = Math.Min(slotH * 0.3, 8),
                Fill = (Brush)new BrushConverter().ConvertFromString(ledColor)!
            };
            Canvas.SetLeft(led, w * 0.08); Canvas.SetTop(led, y + slotH / 2.0 - led.Height / 2.0);
            c.Children.Add(led);

            // Detail line
            var line = new System.Windows.Shapes.Line
            {
                X1 = w * 0.22, Y1 = y + slotH / 2.0,
                X2 = w * 0.85, Y2 = y + slotH / 2.0,
                Stroke = stroke, StrokeThickness = 1, Opacity = 0.45
            };
            c.Children.Add(line);
        }
        return (c, styled.ToArray());
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildPerson(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        // Head (circle) + body (rounded shoulders)
        var headRadius = Math.Min(w, h) * 0.18;
        var head = new System.Windows.Shapes.Ellipse
        {
            Width = headRadius * 2, Height = headRadius * 2,
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness
        };
        Canvas.SetLeft(head, w / 2.0 - headRadius);
        Canvas.SetTop(head, h * 0.05);

        // Body: rounded top, flat bottom (shoulders)
        var bodyTop = h * 0.05 + headRadius * 2 + h * 0.04;
        var bodyHeight = h - bodyTop;
        var bodyWidth = w * 0.85;
        var bodyLeft = (w - bodyWidth) / 2.0;
        var bodyTopRadius = bodyWidth / 2.0;
        var body = new Path
        {
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness,
            Data = Geometry.Parse(FormattableString.Invariant($"M {bodyLeft},{h} L {bodyLeft},{bodyTop + bodyTopRadius} A {bodyTopRadius},{bodyTopRadius} 0 0 1 {bodyLeft + bodyWidth},{bodyTop + bodyTopRadius} L {bodyLeft + bodyWidth},{h} Z"))
        };
        c.Children.Add(body);
        c.Children.Add(head);
        return (c, new System.Windows.Shapes.Shape[] { head, body });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildQueue(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h, Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness, RadiusX = 4, RadiusY = 4
        };
        c.Children.Add(rect);

        // Three vertical dividers
        for (int i = 1; i <= 3; i++)
        {
            var x = (w * i) / 4.0;
            var divider = new System.Windows.Shapes.Line
            {
                X1 = x, Y1 = 4, X2 = x, Y2 = h - 4,
                Stroke = stroke, StrokeThickness = 1.2, Opacity = 0.7
            };
            c.Children.Add(divider);
        }
        return (c, new System.Windows.Shapes.Shape[] { rect });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildNote(Canvas c, double w, double h, Brush fill, Brush stroke)
    {
        // Default note color = soft amber if fill is white
        var fillBrush = fill is SolidColorBrush sc && sc.Color == Colors.White
            ? (Brush)new BrushConverter().ConvertFromString("#FEF3C7")!
            : fill;
        var fold = Math.Min(w, h) * 0.18;
        // Body with folded corner cut
        var body = new Path
        {
            Fill = fillBrush, Stroke = stroke, StrokeThickness = StrokeThickness,
            Data = Geometry.Parse(FormattableString.Invariant($"M 0,0 L {w - fold},0 L {w},{fold} L {w},{h} L 0,{h} Z"))
        };
        var foldTri = new Path
        {
            Fill = Brushes.White, Stroke = stroke, StrokeThickness = StrokeThickness,
            Data = Geometry.Parse(FormattableString.Invariant($"M {w - fold},0 L {w - fold},{fold} L {w},{fold} Z"))
        };
        c.Children.Add(body);
        c.Children.Add(foldTri);
        return (c, new System.Windows.Shapes.Shape[] { body });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildText(Canvas c, double w, double h)
    {
        // Faint dashed frame so the user can find / hit the text shape.
        // Becomes the accent color when selected via SetSelected.
        var hit = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h,
            Fill = Brushes.Transparent,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#CBD5E1")!,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection(new[] { 3.0, 3.0 }),
            Opacity = 0.55
        };
        c.Children.Add(hit);
        return (c, new System.Windows.Shapes.Shape[] { hit });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildRichText(Canvas c, double w, double h, Brush fill, Brush stroke, string? rtf, string? fallback)
    {
        // Rich-text shapes sit on a light card so the formatted content stays legible.
        var fillBrush = fill is SolidColorBrush sc && sc.Color == Colors.White
            ? (Brush)new BrushConverter().ConvertFromString("#FFFFFF")!
            : fill;
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h,
            Fill = fillBrush, Stroke = stroke, StrokeThickness = StrokeThickness,
            RadiusX = 8, RadiusY = 8,
        };
        Canvas.SetLeft(rect, 0); Canvas.SetTop(rect, 0);
        c.Children.Add(rect);

        // A read-only RichTextBox renders the RTF. IsHitTestVisible=false keeps the canvas's
        // manual hit-testing/drag model intact — editing happens via an overlay editor.
        var rtb = new RichTextBox
        {
            Width = w, Height = h,
            IsReadOnly = true, IsHitTestVisible = false, Focusable = false,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = (Brush)new BrushConverter().ConvertFromString("#0F172A")!,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        rtb.Document.PagePadding = new Thickness(0);
        LoadRich(rtb, rtf, fallback);
        Canvas.SetLeft(rtb, 0); Canvas.SetTop(rtb, 0);
        c.Children.Add(rtb);

        return (c, new System.Windows.Shapes.Shape[] { rect });
    }

    /// Loads RTF into a RichTextBox, falling back to plain text when there's no RTF yet.
    public static void LoadRich(RichTextBox rtb, string? rtf, string? fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(rtf))
            {
                var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                var bytes = System.Text.Encoding.UTF8.GetBytes(rtf);
                using var ms = new System.IO.MemoryStream(bytes);
                range.Load(ms, DataFormats.Rtf);
                return;
            }
        }
        catch { /* corrupt/empty RTF: fall through to plain text */ }
        rtb.Document.Blocks.Clear();
        rtb.Document.Blocks.Add(new Paragraph(new Run(fallback ?? "")));
    }

    /// Serializes a RichTextBox's document back to an RTF string.
    public static string SaveRich(RichTextBox rtb)
    {
        var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
        using var ms = new System.IO.MemoryStream();
        range.Save(ms, DataFormats.Rtf);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildImage(Canvas c, double w, double h, string? dataUrl)
    {
        try
        {
            if (!string.IsNullOrEmpty(dataUrl))
            {
                var comma = dataUrl.IndexOf(',');
                var b64 = comma >= 0 ? dataUrl.Substring(comma + 1) : dataUrl;
                var bytes = Convert.FromBase64String(b64);
                var bmp = new BitmapImage();
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                c.Children.Add(new Image { Source = bmp, Width = w, Height = h, Stretch = Stretch.Fill });
                return (c, Array.Empty<System.Windows.Shapes.Shape>());
            }
        }
        catch { /* fall through to a placeholder */ }

        // Missing or undecodable image: dashed placeholder box.
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h,
            Fill = (Brush)new BrushConverter().ConvertFromString("#F1F5F9")!,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#CBD5E1")!,
            StrokeThickness = StrokeThickness,
            StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 })
        };
        c.Children.Add(rect);
        return (c, new System.Windows.Shapes.Shape[] { rect });
    }

    private static (UIElement, System.Windows.Shapes.Shape[]) BuildServiceTile(Canvas c, double w, double h, Brush fill, Brush stroke, string? stencil)
    {
        // White rounded tile with a provider-colored border.
        var tile = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = h,
            Fill = fill, Stroke = stroke, StrokeThickness = StrokeThickness,
            RadiusX = 10, RadiusY = 10
        };
        Canvas.SetLeft(tile, 0); Canvas.SetTop(tile, 0);
        c.Children.Add(tile);

        // Provider-colored badge near the top holding a generic category glyph.
        var badgeSize = Math.Min(w, h) * 0.4;
        var bx = (w - badgeSize) / 2.0;
        var by = h * 0.1;
        var badge = new System.Windows.Shapes.Rectangle
        {
            Width = badgeSize, Height = badgeSize,
            Fill = stroke,
            RadiusX = badgeSize * 0.22, RadiusY = badgeSize * 0.22
        };
        Canvas.SetLeft(badge, bx); Canvas.SetTop(badge, by);
        c.Children.Add(badge);

        var category = Stencils.Find(stencil)?.Category ?? "compute";
        var glyphBox = badgeSize * 0.6;
        var glyph = BuildCategoryGlyph(category, glyphBox, Brushes.White);
        Canvas.SetLeft(glyph, bx + (badgeSize - glyphBox) / 2.0);
        Canvas.SetTop(glyph, by + (badgeSize - glyphBox) / 2.0);
        c.Children.Add(glyph);

        // Only the tile border re-themes on selection; the badge keeps its provider color.
        return (c, new System.Windows.Shapes.Shape[] { tile });
    }

    /// A small standalone badge (provider-colored square + white category glyph) for menus/lists.
    public static UIElement BuildServiceBadge(string? stencil, double size, Brush color)
    {
        var c = new Canvas { Width = size, Height = size, IsHitTestVisible = false };
        c.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = size, Height = size, Fill = color,
            RadiusX = size * 0.22, RadiusY = size * 0.22
        });
        var category = Stencils.Find(stencil)?.Category ?? "compute";
        var gbox = size * 0.62;
        var glyph = BuildCategoryGlyph(category, gbox, Brushes.White);
        Canvas.SetLeft(glyph, (size - gbox) / 2);
        Canvas.SetTop(glyph, (size - gbox) / 2);
        c.Children.Add(glyph);
        return c;
    }

    /// Generic, original line glyphs (drawn white) for each service category — not provider artwork.
    private static UIElement BuildCategoryGlyph(string category, double box, Brush color)
    {
        var g = new Canvas { Width = box, Height = box, IsHitTestVisible = false };
        double st = Math.Max(1.3, box * 0.085);
        double B(double t) => t * box;

        void Line(double x1, double y1, double x2, double y2) =>
            g.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = B(x1), Y1 = B(y1), X2 = B(x2), Y2 = B(y2),
                Stroke = color, StrokeThickness = st,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });

        void Ell(double cx, double cy, double rx, double ry)
        {
            var e = new System.Windows.Shapes.Ellipse
            {
                Width = B(rx * 2), Height = B(ry * 2),
                Stroke = color, StrokeThickness = st, Fill = null
            };
            Canvas.SetLeft(e, B(cx - rx)); Canvas.SetTop(e, B(cy - ry));
            g.Children.Add(e);
        }

        void RoundRect(double x, double y, double rw, double rh, double rad)
        {
            var r = new System.Windows.Shapes.Rectangle
            {
                Width = B(rw), Height = B(rh),
                Stroke = color, StrokeThickness = st, Fill = null,
                RadiusX = B(rad), RadiusY = B(rad)
            };
            Canvas.SetLeft(r, B(x)); Canvas.SetTop(r, B(y));
            g.Children.Add(r);
        }

        void FillRect(double x, double y, double rw, double rh)
        {
            var r = new System.Windows.Shapes.Rectangle { Width = B(rw), Height = B(rh), Fill = color };
            Canvas.SetLeft(r, B(x)); Canvas.SetTop(r, B(y));
            g.Children.Add(r);
        }

        void Poly(bool fill, bool closed, params double[] xy)
        {
            var pts = new PointCollection();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Point(B(xy[i]), B(xy[i + 1])));
            if (closed)
                g.Children.Add(new Polygon
                {
                    Points = pts, Stroke = fill ? null : color, StrokeThickness = fill ? 0 : st,
                    Fill = fill ? color : null, StrokeLineJoin = PenLineJoin.Round
                });
            else
                g.Children.Add(new Polyline
                {
                    Points = pts, Stroke = color, StrokeThickness = st, Fill = null,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                });
        }

        switch (category)
        {
            case "function": // lightning bolt
                Poly(fill: true, closed: true, 0.56, 0.08, 0.30, 0.54, 0.46, 0.54, 0.40, 0.92, 0.72, 0.44, 0.54, 0.44);
                break;
            case "storage": // bucket + rim
                Poly(fill: false, closed: true, 0.28, 0.34, 0.72, 0.34, 0.64, 0.82, 0.36, 0.82);
                Ell(0.5, 0.34, 0.22, 0.06);
                break;
            case "database": // cylinder
                Ell(0.5, 0.30, 0.20, 0.06);
                Line(0.30, 0.30, 0.30, 0.70);
                Line(0.70, 0.30, 0.70, 0.70);
                g.Children.Add(new Path
                {
                    Data = Geometry.Parse(FormattableString.Invariant($"M {B(0.30)},{B(0.70)} A {B(0.20)},{B(0.06)} 0 0 0 {B(0.70)},{B(0.70)}")),
                    Stroke = color, StrokeThickness = st, Fill = null
                });
                break;
            case "container": // 3D cube
                Poly(fill: false, closed: true, 0.30, 0.42, 0.62, 0.42, 0.62, 0.80, 0.30, 0.80); // front
                Poly(fill: false, closed: true, 0.30, 0.42, 0.42, 0.28, 0.74, 0.28, 0.62, 0.42); // top
                Poly(fill: false, closed: true, 0.62, 0.42, 0.74, 0.28, 0.74, 0.66, 0.62, 0.80); // side
                break;
            case "messaging": // envelope
                RoundRect(0.22, 0.34, 0.56, 0.34, 0.03);
                Poly(fill: false, closed: false, 0.23, 0.36, 0.50, 0.56, 0.77, 0.36);
                break;
            case "network": // globe
                Ell(0.5, 0.5, 0.26, 0.26);
                Ell(0.5, 0.5, 0.10, 0.26);
                Line(0.24, 0.5, 0.76, 0.5);
                break;
            case "analytics": // bar chart
                FillRect(0.28, 0.56, 0.10, 0.24);
                FillRect(0.45, 0.36, 0.10, 0.44);
                FillRect(0.62, 0.48, 0.10, 0.32);
                Line(0.24, 0.82, 0.76, 0.82);
                break;
            case "monitoring": // pulse / heartbeat
                Poly(fill: false, closed: false, 0.18, 0.5, 0.36, 0.5, 0.44, 0.28, 0.56, 0.74, 0.64, 0.5, 0.82, 0.5);
                break;
            default: // compute — CPU chip
                RoundRect(0.30, 0.30, 0.40, 0.40, 0.05);
                Line(0.42, 0.30, 0.42, 0.20); Line(0.58, 0.30, 0.58, 0.20);
                Line(0.42, 0.70, 0.42, 0.80); Line(0.58, 0.70, 0.58, 0.80);
                Line(0.30, 0.42, 0.20, 0.42); Line(0.30, 0.58, 0.20, 0.58);
                Line(0.70, 0.42, 0.80, 0.42); Line(0.70, 0.58, 0.80, 0.58);
                break;
        }
        return g;
    }

    /// Compute where a connector line should attach to a shape edge along the ray from inside the shape toward an external point.
    public static Point EdgeIntersect(ShapeNode node, Point external)
    {
        var cx = node.CenterX;
        var cy = node.CenterY;
        var dx = external.X - cx;
        var dy = external.Y - cy;
        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) return new Point(cx, cy);

        switch (node.Kind)
        {
            case ShapeKind.Ellipse:
            case ShapeKind.Person:
            {
                // Ellipse intersection
                var a = node.Width / 2.0;
                var b = node.Height / 2.0;
                var t = 1.0 / Math.Sqrt((dx * dx) / (a * a) + (dy * dy) / (b * b));
                return new Point(cx + dx * t, cy + dy * t);
            }
            case ShapeKind.Diamond:
            {
                // Diamond as 4 line segments — find intersection with the half toward external
                var halfW = node.Width / 2.0;
                var halfH = node.Height / 2.0;
                // Diamond edge equation: |x-cx|/halfW + |y-cy|/halfH = 1
                var t = 1.0 / (Math.Abs(dx) / halfW + Math.Abs(dy) / halfH);
                return new Point(cx + dx * t, cy + dy * t);
            }
            default:
            {
                // Rectangle bounding box intersection
                var halfW = node.Width / 2.0;
                var halfH = node.Height / 2.0;
                var sx = dx != 0 ? halfW / Math.Abs(dx) : double.MaxValue;
                var sy = dy != 0 ? halfH / Math.Abs(dy) : double.MaxValue;
                var t = Math.Min(sx, sy);
                return new Point(cx + dx * t, cy + dy * t);
            }
        }
    }
}

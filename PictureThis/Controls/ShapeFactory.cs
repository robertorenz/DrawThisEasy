using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PictureThis.Models;

namespace PictureThis.Controls;

/// Builds the visual for a single shape (geometry + label) inside a Canvas of size w x h.
public static class ShapeFactory
{
    public const double StrokeThickness = 1.6;

    /// Renders the shape body into the supplied panel. Returns a list of shapes whose Fill/Stroke should be updated when colors change.
    public static (UIElement Container, System.Windows.Shapes.Shape[] StyledParts) BuildBody(ShapeKind kind, double w, double h, Brush fill, Brush stroke)
    {
        var canvas = new Canvas { Width = w, Height = h, IsHitTestVisible = true, Background = Brushes.Transparent };

        return kind switch
        {
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
            Data = Geometry.Parse($"M 0,{ry} L 0,{h - ry} A {rx},{ry} 0 0 0 {w},{h - ry} L {w},{ry} Z")
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
            Data = Geometry.Parse($"M 0,{ry} L 0,{h - ry} A {rx},{ry} 0 0 0 {w},{h - ry} L {w},{ry}")
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
            Data = Geometry.Parse($"M {bodyLeft},{h} L {bodyLeft},{bodyTop + bodyTopRadius} A {bodyTopRadius},{bodyTopRadius} 0 0 1 {bodyLeft + bodyWidth},{bodyTop + bodyTopRadius} L {bodyLeft + bodyWidth},{h} Z")
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
            Data = Geometry.Parse($"M 0,0 L {w - fold},0 L {w},{fold} L {w},{h} L 0,{h} Z")
        };
        var foldTri = new Path
        {
            Fill = Brushes.White, Stroke = stroke, StrokeThickness = StrokeThickness,
            Data = Geometry.Parse($"M {w - fold},0 L {w - fold},{fold} L {w},{fold} Z")
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

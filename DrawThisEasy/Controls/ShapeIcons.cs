using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawThisEasy.Models;

namespace DrawThisEasy.Controls;

/// Small 18x18-ish icons used in the palette and template previews.
public static class ShapeIcons
{
    public static UIElement GetIcon(ToolMode mode, Brush brush)
    {
        var canvas = new Canvas { Width = 18, Height = 18 };
        var pen = brush;
        switch (mode)
        {
            case ToolMode.Select:
                Add(canvas, new Polygon
                {
                    Points = new PointCollection(new[] { new Point(3, 1), new Point(15, 8), new Point(9, 9), new Point(7, 16) }),
                    Fill = brush
                });
                break;

            case ToolMode.Connect:
                Add(canvas, new Ellipse { Width = 5, Height = 5, Stroke = pen, StrokeThickness = 1.4 }, 1, 1);
                Add(canvas, new Ellipse { Width = 5, Height = 5, Stroke = pen, StrokeThickness = 1.4 }, 12, 12);
                Add(canvas, new Line { X1 = 5, Y1 = 5, X2 = 13, Y2 = 13, Stroke = pen, StrokeThickness = 1.4 });
                break;

            case ToolMode.Pan:
                Add(canvas, new Path { Data = Geometry.Parse("M 9,2 L 9,16 M 2,9 L 16,9 M 4,5 L 4,5 M 14,5 L 14,5"), Stroke = pen, StrokeThickness = 1.4, Fill = null });
                break;

            case ToolMode.AddRectangle:
                Add(canvas, new System.Windows.Shapes.Rectangle { Width = 14, Height = 10, Stroke = pen, StrokeThickness = 1.4 }, 2, 4);
                break;

            case ToolMode.AddRounded:
                Add(canvas, new System.Windows.Shapes.Rectangle { Width = 14, Height = 10, Stroke = pen, StrokeThickness = 1.4, RadiusX = 4, RadiusY = 4 }, 2, 4);
                break;

            case ToolMode.AddEllipse:
                Add(canvas, new Ellipse { Width = 14, Height = 10, Stroke = pen, StrokeThickness = 1.4 }, 2, 4);
                break;

            case ToolMode.AddDiamond:
                Add(canvas, new Polygon
                {
                    Points = new PointCollection(new[] { new Point(9, 2), new Point(16, 9), new Point(9, 16), new Point(2, 9) }),
                    Stroke = pen, StrokeThickness = 1.4, Fill = null
                });
                break;

            case ToolMode.AddHexagon:
                Add(canvas, new Polygon
                {
                    Points = new PointCollection(new[] { new Point(5, 3), new Point(13, 3), new Point(17, 9), new Point(13, 15), new Point(5, 15), new Point(1, 9) }),
                    Stroke = pen, StrokeThickness = 1.4, Fill = null
                });
                break;

            case ToolMode.AddParallelogram:
                Add(canvas, new Polygon
                {
                    Points = new PointCollection(new[] { new Point(5, 3), new Point(17, 3), new Point(13, 15), new Point(1, 15) }),
                    Stroke = pen, StrokeThickness = 1.4, Fill = null
                });
                break;

            case ToolMode.AddCylinder:
                Add(canvas, new Ellipse { Width = 14, Height = 4, Stroke = pen, StrokeThickness = 1.4 }, 2, 2);
                Add(canvas, new Path { Data = Geometry.Parse("M 2,4 L 2,13 C 2,14.5 5,15.5 9,15.5 C 13,15.5 16,14.5 16,13 L 16,4"), Stroke = pen, StrokeThickness = 1.4, Fill = null });
                break;

            case ToolMode.AddCloud:
                Add(canvas, new Path
                {
                    Data = Geometry.Parse("M 5,13 C 2,13 1,11 2,10 C 1,8 3,6 5,7 C 5,4 8,3 10,5 C 13,3 15,5 14,7 C 16,7 17,9 16,11 C 17,12 15,14 13,13 Z"),
                    Stroke = pen, StrokeThickness = 1.4, Fill = null
                });
                break;

            case ToolMode.AddServer:
                for (int i = 0; i < 3; i++)
                {
                    Add(canvas, new System.Windows.Shapes.Rectangle { Width = 14, Height = 3.5, Stroke = pen, StrokeThickness = 1.2, RadiusX = 1, RadiusY = 1 }, 2, 2 + i * 5);
                    Add(canvas, new Ellipse { Width = 1.6, Height = 1.6, Fill = brush }, 4, 3.2 + i * 5);
                }
                break;

            case ToolMode.AddPerson:
                Add(canvas, new Ellipse { Width = 5, Height = 5, Stroke = pen, StrokeThickness = 1.4 }, 6.5, 1.5);
                Add(canvas, new Path { Data = Geometry.Parse("M 2,17 C 2,12 5,9.5 9,9.5 C 13,9.5 16,12 16,17"), Stroke = pen, StrokeThickness = 1.4, Fill = null });
                break;

            case ToolMode.AddQueue:
                Add(canvas, new System.Windows.Shapes.Rectangle { Width = 14, Height = 6, Stroke = pen, StrokeThickness = 1.4 }, 2, 6);
                Add(canvas, new Line { X1 = 6, Y1 = 7, X2 = 6, Y2 = 11, Stroke = pen, StrokeThickness = 1.2 });
                Add(canvas, new Line { X1 = 9, Y1 = 7, X2 = 9, Y2 = 11, Stroke = pen, StrokeThickness = 1.2 });
                Add(canvas, new Line { X1 = 12, Y1 = 7, X2 = 12, Y2 = 11, Stroke = pen, StrokeThickness = 1.2 });
                break;

            case ToolMode.AddNote:
                Add(canvas, new Path { Data = Geometry.Parse("M 2,2 L 13,2 L 16,5 L 16,16 L 2,16 Z M 13,2 L 13,5 L 16,5"), Stroke = pen, StrokeThickness = 1.4, Fill = null });
                break;

            case ToolMode.AddText:
                Add(canvas, new TextBlock
                {
                    Text = "T",
                    FontSize = 14, FontWeight = FontWeights.Bold,
                    Foreground = brush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 18, Height = 18, TextAlignment = TextAlignment.Center
                });
                break;
        }
        return canvas;
    }

    private static void Add(Canvas c, UIElement el, double left = 0, double top = 0)
    {
        Canvas.SetLeft(el, left);
        Canvas.SetTop(el, top);
        c.Children.Add(el);
    }
}

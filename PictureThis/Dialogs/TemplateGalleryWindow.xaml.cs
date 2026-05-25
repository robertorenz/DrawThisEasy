using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PictureThis.Controls;
using PictureThis.Models;
using PictureThis.Services;

namespace PictureThis.Dialogs;

public partial class TemplateGalleryWindow : Window
{
    public DiagramTemplate? SelectedTemplate { get; private set; }

    public TemplateGalleryWindow()
    {
        InitializeComponent();
        foreach (var t in Templates.All())
            TemplatesGrid.Items.Add(BuildCard(t));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private Border BuildCard(DiagramTemplate t)
    {
        var preview = BuildPreview(t.Builder);
        var meta = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
        meta.Children.Add(new TextBlock
        {
            Text = t.Title,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 2)
        });
        meta.Children.Add(new TextBlock
        {
            Text = t.Description,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap
        });

        var body = new DockPanel();
        var previewBorder = new Border
        {
            Background = (Brush)FindResource("CanvasBgBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 130,
            Child = preview,
            ClipToBounds = true
        };
        DockPanel.SetDock(previewBorder, Dock.Top);
        body.Children.Add(previewBorder);
        body.Children.Add(meta);

        var card = new Border
        {
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 12, 12),
            Cursor = Cursors.Hand,
            Child = body
        };
        card.MouseLeftButtonDown += (s, e) =>
        {
            SelectedTemplate = t;
            DialogResult = true;
            Close();
        };
        card.MouseEnter += (s, e) =>
        {
            card.BorderBrush = (Brush)FindResource("AccentBrush");
        };
        card.MouseLeave += (s, e) =>
        {
            card.BorderBrush = (Brush)FindResource("BorderBrush");
        };
        return card;
    }

    // Build a small visual preview of the diagram (read-only, scaled to fit).
    private UIElement BuildPreview(DiagramModel m)
    {
        var canvas = new Canvas
        {
            Background = Brushes.Transparent,
            Width = 240,
            Height = 130
        };

        if (m.Shapes.Count == 0)
        {
            canvas.Children.Add(new TextBlock
            {
                Text = "Empty canvas",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(0)
            });
            Canvas.SetLeft(canvas.Children[0], 90);
            Canvas.SetTop(canvas.Children[0], 58);
            return canvas;
        }

        // Compute bounding box
        var minX = m.Shapes.Min(s => s.X);
        var minY = m.Shapes.Min(s => s.Y);
        var maxX = m.Shapes.Max(s => s.X + s.Width);
        var maxY = m.Shapes.Max(s => s.Y + s.Height);
        var w = maxX - minX;
        var h = maxY - minY;
        var sx = (canvas.Width - 16) / w;
        var sy = (canvas.Height - 16) / h;
        var scale = Math.Min(sx, sy);
        var offsetX = (canvas.Width - w * scale) / 2 - minX * scale;
        var offsetY = (canvas.Height - h * scale) / 2 - minY * scale;

        // Connections first (so they render under shapes)
        foreach (var c in m.Connections)
        {
            var from = m.FindShape(c.FromId);
            var to = m.FindShape(c.ToId);
            if (from == null || to == null) continue;
            var pFrom = new Point(from.CenterX * scale + offsetX, from.CenterY * scale + offsetY);
            var pTo = new Point(to.CenterX * scale + offsetX, to.CenterY * scale + offsetY);
            var line = new System.Windows.Shapes.Line
            {
                X1 = pFrom.X, Y1 = pFrom.Y, X2 = pTo.X, Y2 = pTo.Y,
                Stroke = (Brush)new BrushConverter().ConvertFromString("#94A3B8")!,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
        }

        foreach (var s in m.Shapes.OrderBy(s => s.ZIndex))
        {
            var fill = (Brush)new BrushConverter().ConvertFromString(s.Fill)!;
            var stroke = (Brush)new BrushConverter().ConvertFromString(s.Stroke)!;
            var (body, _) = ShapeFactory.BuildBody(s.Kind, s.Width * scale, s.Height * scale, fill, stroke);
            Canvas.SetLeft(body, s.X * scale + offsetX);
            Canvas.SetTop(body, s.Y * scale + offsetY);
            canvas.Children.Add(body);
        }

        return canvas;
    }
}

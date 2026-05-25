using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PictureThis.Services;

public static class Exporter
{
    /// Render a Visual to a PNG file at a given scale (1.0 = screen size, 2.0 = retina).
    public static void ExportPng(FrameworkElement element, string path, double scale = 2.0, Brush? background = null)
    {
        if (element.ActualWidth < 1 || element.ActualHeight < 1)
            throw new InvalidOperationException("Element has zero size; cannot export.");

        var dpi = 96 * scale;
        var width = (int)Math.Ceiling(element.ActualWidth * scale);
        var height = (int)Math.Ceiling(element.ActualHeight * scale);

        var rtb = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);

        // Render through a visual brush so we get the entire element painted on a known background
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            if (background != null)
                ctx.DrawRectangle(background, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var brush = new VisualBrush(element) { Stretch = Stretch.None };
            ctx.DrawRectangle(brush, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}

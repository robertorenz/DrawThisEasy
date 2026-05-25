using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PictureThis.Services;

namespace PictureThis.Dialogs;

public partial class ColorPickerWindow : Window
{
    private double _hue;   // 0-360
    private double _sat;   // 0-1
    private double _val;   // 0-1
    private bool _svDrag, _hueDrag;
    private bool _suppressHexEvent;

    public Color SelectedColor { get; private set; } = Colors.White;

    public ColorPickerWindow()
    {
        InitializeComponent();
        TitleText.Text   = L10n.T("color.title");
        LblHex.Text      = L10n.T("color.hex");
        LblStandard.Text = L10n.T("color.standard");
        BtnCancel.Content = L10n.T("modal.cancel");
        BtnApply.Content  = L10n.T("color.apply");
        BuildStandardPalette();
        Loaded += (_, _) => Refresh();
    }

    public static Color? Pick(Window owner, Color initial)
    {
        var w = new ColorPickerWindow { Owner = owner };
        w.LoadColor(initial);
        return w.ShowDialog() == true ? w.SelectedColor : null;
    }

    // ---------- Standard palette ----------

    private static readonly string[] StandardPalette = new[]
    {
        // Row 1 — grayscale (white → black)
        "#FFFFFF","#F8FAFC","#F1F5F9","#E2E8F0","#CBD5E1","#94A3B8","#64748B","#475569","#1E293B","#0F172A",
        // Row 2 — reds / roses
        "#FEE2E2","#FCA5A5","#F87171","#EF4444","#B91C1C","#FFE4E6","#FDA4AF","#F43F5E","#BE123C","#7F1D1D",
        // Row 3 — oranges / ambers
        "#FFEDD5","#FDBA74","#F97316","#EA580C","#9A3412","#FEF3C7","#FCD34D","#F59E0B","#B45309","#78350F",
        // Row 4 — yellows / greens
        "#FEF9C3","#FDE047","#FACC15","#CA8A04","#854D0E","#D1FAE5","#6EE7B7","#10B981","#047857","#064E3B",
        // Row 5 — teals / cyans
        "#CCFBF1","#5EEAD4","#14B8A6","#0F766E","#134E4A","#CFFAFE","#67E8F9","#06B6D4","#0E7490","#164E63",
        // Row 6 — blues
        "#DBEAFE","#93C5FD","#3B82F6","#1D4ED8","#1E3A8A","#E0F2FE","#7DD3FC","#0EA5E9","#0369A1","#0C4A6E",
    };

    private void BuildStandardPalette()
    {
        foreach (var hex in StandardPalette)
        {
            var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
            var border = new Border
            {
                Width = 26, Height = 26,
                Margin = new Thickness(0, 0, 2, 2),
                CornerRadius = new CornerRadius(4),
                Background = brush,
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = hex
            };
            border.MouseLeftButtonDown += (_, _) =>
            {
                LoadColor((Color)ColorConverter.ConvertFromString(hex));
            };
            StandardGrid.Items.Add(border);
        }
    }

    // ---------- Color state ----------

    private void LoadColor(Color c)
    {
        SelectedColor = c;
        (_hue, _sat, _val) = RgbToHsv(c);
        Refresh();
    }

    private void Refresh()
    {
        // Base saturation gradient: white → fully-saturated hue
        var fullHue = HsvToRgb(_hue, 1, 1);
        SVBase.Fill = new LinearGradientBrush(Colors.White, fullHue, new Point(0, 0), new Point(1, 0));

        // SV cursor position
        if (SVPicker.ActualWidth > 0 && SVPicker.ActualHeight > 0)
        {
            Canvas.SetLeft(SVCursor, _sat * SVPicker.ActualWidth - 7);
            Canvas.SetTop(SVCursor, (1 - _val) * SVPicker.ActualHeight - 7);
        }

        // Hue cursor position
        if (HueSlider.ActualWidth > 0)
        {
            Canvas.SetLeft(HueCursor, _hue / 360.0 * HueSlider.ActualWidth - 3);
        }

        SelectedColor = HsvToRgb(_hue, _sat, _val);
        PreviewSwatch.Fill = new SolidColorBrush(SelectedColor);

        _suppressHexEvent = true;
        HexInput.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        _suppressHexEvent = false;
    }

    // ---------- Mouse — SV picker ----------

    private void SVPicker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _svDrag = true;
        SVPicker.CaptureMouse();
        UpdateSVFromPoint(e.GetPosition(SVPicker));
    }

    private void SVPicker_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_svDrag || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateSVFromPoint(e.GetPosition(SVPicker));
    }

    private void SVPicker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _svDrag = false;
        SVPicker.ReleaseMouseCapture();
    }

    private void UpdateSVFromPoint(Point p)
    {
        var w = Math.Max(1, SVPicker.ActualWidth);
        var h = Math.Max(1, SVPicker.ActualHeight);
        _sat = Math.Clamp(p.X / w, 0, 1);
        _val = Math.Clamp(1 - p.Y / h, 0, 1);
        Refresh();
    }

    // ---------- Mouse — Hue slider ----------

    private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _hueDrag = true;
        HueSlider.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(HueSlider));
    }

    private void Hue_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_hueDrag || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateHueFromPoint(e.GetPosition(HueSlider));
    }

    private void Hue_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _hueDrag = false;
        HueSlider.ReleaseMouseCapture();
    }

    private void UpdateHueFromPoint(Point p)
    {
        var w = Math.Max(1, HueSlider.ActualWidth);
        _hue = Math.Clamp(p.X / w, 0, 1) * 360.0;
        if (_hue >= 360) _hue = 359.999;
        Refresh();
    }

    // ---------- Hex input ----------

    private void HexInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryApplyHex();
            e.Handled = true;
        }
    }

    private void HexInput_LostFocus(object sender, RoutedEventArgs e) => TryApplyHex();

    private void TryApplyHex()
    {
        if (_suppressHexEvent) return;
        var text = HexInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        if (!text.StartsWith("#")) text = "#" + text;
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(text);
            LoadColor(c);
        }
        catch
        {
            // restore from current state
            Refresh();
        }
    }

    // ---------- Buttons ----------

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ---------- HSV helpers ----------

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r)       h = (((g - b) / delta) % 6 + 6) % 6;
            else if (max == g)  h = (b - r) / delta + 2;
            else                h = (r - g) / delta + 4;
            h *= 60;
        }
        double s = max == 0 ? 0 : delta / max;
        double v = max;
        return (h, s, v);
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
        double m = v - c;
        double r, g, b;
        if (h < 60)       { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else              { r = c; g = 0; b = x; }
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}

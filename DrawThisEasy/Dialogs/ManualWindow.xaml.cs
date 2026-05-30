using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawThisEasy.Controls;
using DrawThisEasy.Models;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

public partial class ManualWindow : Window
{
    private static readonly Brush HeadingBrush = Brush("#0F172A");
    private static readonly Brush BodyBrush    = Brush("#334155");
    private static readonly Brush AccentBrush  = Brush("#0EA5E9");

    // Chapter navigation: heading key (in order) → its heading element, for scroll-to.
    private readonly System.Collections.Generic.List<string> _chapterKeys = new();
    private readonly System.Collections.Generic.Dictionary<string, FrameworkElement> _anchors = new();

    public ManualWindow()
    {
        InitializeComponent();
        TitleText.Text    = L10n.T("manual.title");
        SubtitleText.Text = L10n.T("manual.subtitle");
        BtnClose.Content  = L10n.T("help.close");
        Build();
    }

    public static void Show(Window owner)
    {
        var w = new ManualWindow { Owner = owner };
        w.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // The three core tools (icon + name + description, reusing the palette strings).
    private static readonly (ToolMode Mode, string Name, string Desc)[] CoreTools =
    {
        (ToolMode.Select,  "tool.select",  "tip.select"),
        (ToolMode.Connect, "tool.connect", "tip.connect"),
        (ToolMode.Pan,     "tool.pan",     "tip.pan"),
    };

    // The palette shapes, rendered as live previews in the gallery.
    private static readonly (ShapeKind Kind, string Name, double W, double H)[] Shapes =
    {
        (ShapeKind.Rectangle,     "tool.process",   84, 44),
        (ShapeKind.Rounded,       "tool.component", 84, 44),
        (ShapeKind.Ellipse,       "tool.startend",  84, 46),
        (ShapeKind.Diamond,       "tool.decision",  70, 52),
        (ShapeKind.Hexagon,       "tool.hexagon",   88, 46),
        (ShapeKind.Parallelogram, "tool.data",      84, 44),
        (ShapeKind.Cylinder,      "tool.database",  60, 50),
        (ShapeKind.Cloud,         "tool.cloud",     82, 50),
        (ShapeKind.Server,        "tool.server",    52, 58),
        (ShapeKind.Person,        "tool.user",      46, 58),
        (ShapeKind.Queue,         "tool.queue",     90, 36),
        (ShapeKind.Note,          "tool.note",      58, 52),
        (ShapeKind.Text,          "tool.text",      72, 30),
        (ShapeKind.RichText,      "tool.richtext",  92, 50),
    };

    private void Build()
    {
        Heading("manual.overview.h", first: true);
        Para("manual.overview.p");

        Heading("manual.workspace.h");
        Para("manual.workspace.p");
        Bullets("manual.workspace.b.strip", "manual.workspace.b.favorites", "manual.workspace.b.palette", "manual.workspace.b.canvas",
                "manual.workspace.b.inspector", "manual.workspace.b.status");

        Heading("manual.tools.h");
        Para("manual.tools.p");
        foreach (var (mode, name, desc) in CoreTools)
            ManualStack.Children.Add(MakeToolRow(mode, name, desc));

        Heading("manual.shapes.h");
        Para("manual.shapes.p");
        ManualStack.Children.Add(MakeShapeGallery());
        Para("manual.shapes.p3");

        Heading("manual.connect.h");
        Para("manual.connect.p");
        Para("manual.connect.p2");

        Heading("manual.select.h");
        Bullets("manual.select.b.click", "manual.select.b.multi", "manual.select.b.marquee",
                "manual.select.b.pan", "manual.select.b.resize");

        Heading("manual.colors.h");
        Para("manual.colors.p");

        Heading("manual.text.h");
        Para("manual.text.p");
        Bullets("manual.text.b.font", "manual.text.b.rich", "manual.text.b.edit");

        Heading("manual.paste.h");
        Para("manual.paste.p");
        Bullets("manual.paste.b.image", "manual.paste.b.rich", "manual.paste.b.text", "manual.paste.b.label");

        Heading("manual.rulers.h");
        Bullets("manual.rulers.b.show", "manual.rulers.b.drag", "manual.rulers.b.move", "manual.rulers.b.snap", "manual.rulers.b.clear");

        Heading("manual.view.h");
        Bullets("manual.view.b.pan", "manual.view.b.zoom", "manual.view.b.reset");

        Heading("manual.present.h");
        Para("manual.present.p");
        Bullets("manual.present.b.mark", "manual.present.b.free", "manual.present.b.manage",
                "manual.present.b.style", "manual.present.b.start", "manual.present.b.keys");

        Heading("manual.toolbars.h");
        Para("manual.toolbars.p");
        Bullets("manual.toolbars.b.pin", "manual.toolbars.b.customize", "manual.toolbars.b.hide", "manual.toolbars.b.reset");

        Heading("manual.prefs.h");
        Para("manual.prefs.p");
        Bullets("manual.prefs.b.connector", "manual.prefs.b.snap", "manual.prefs.b.units", "manual.prefs.b.autosave", "manual.prefs.b.restore");

        Heading("manual.templates.h");
        Para("manual.templates.p");

        Heading("manual.files.h");
        Bullets("manual.files.b.save", "manual.files.b.recent", "manual.files.b.export",
                "manual.files.b.import", "manual.files.b.new");

        Heading("manual.editing.h");
        Para("manual.editing.p");

        Heading("manual.language.h");
        Para("manual.language.p");

        Heading("manual.shortcuts.h");
        Para("manual.shortcuts.p");

        Heading("manual.faq.h");
        for (int i = 1; i <= 11; i++)
            Faq($"manual.faq.q{i}", $"manual.faq.a{i}");

        BuildToc();
    }

    // ---------- content builders ----------

    private void Heading(string key, bool first = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, first ? 0 : 22, 0, 9) };
        panel.Children.Add(new Border { Width = 3, Background = AccentBrush, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 1, 10, 1) });
        panel.Children.Add(new TextBlock
        {
            Text = L10n.T(key), FontFamily = new FontFamily("Segoe UI"), FontSize = 15,
            FontWeight = FontWeights.SemiBold, Foreground = HeadingBrush, VerticalAlignment = VerticalAlignment.Center
        });
        ManualStack.Children.Add(panel);
        _chapterKeys.Add(key);
        _anchors[key] = panel;
    }

    // ---------- chapter navigation + FAQ ----------

    private void BuildToc()
    {
        foreach (var key in _chapterKeys)
        {
            var hoverBrush = Brush("#FFEAF0F6");
            var item = new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = L10n.T(key), FontSize = 12, Foreground = BodyBrush, TextWrapping = TextWrapping.Wrap }
            };
            var captured = key;
            item.MouseEnter += (s, e) => item.Background = hoverBrush;
            item.MouseLeave += (s, e) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonUp += (s, e) => ScrollTo(captured);
            TocStack.Children.Add(item);
        }
    }

    private void ScrollTo(string key)
    {
        if (_anchors.TryGetValue(key, out var el))
        {
            var top = el.TranslatePoint(new Point(0, 0), ManualStack).Y;
            BodyScroll.ScrollToVerticalOffset(top);
        }
    }

    private void Faq(string qKey, string aKey)
    {
        ManualStack.Children.Add(new TextBlock
        {
            Text = L10n.T(qKey), FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            FontWeight = FontWeights.SemiBold, Foreground = HeadingBrush, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(13, 9, 0, 3)
        });
        ManualStack.Children.Add(new TextBlock
        {
            Text = L10n.T(aKey), FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = BodyBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(13, 0, 0, 5)
        });
    }

    private void Para(string key) => ManualStack.Children.Add(new TextBlock
    {
        Text = L10n.T(key), FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
        Foreground = BodyBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(13, 0, 0, 9)
    });

    private void Bullets(params string[] keys)
    {
        foreach (var key in keys)
        {
            var row = new Grid { Margin = new Thickness(13, 0, 0, 7) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var dot = new TextBlock { Text = "•", FontSize = 14, Foreground = AccentBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(dot, 0);
            var body = new TextBlock { Text = L10n.T(key), FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Foreground = BodyBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
            Grid.SetColumn(body, 1);
            row.Children.Add(dot); row.Children.Add(body);
            ManualStack.Children.Add(row);
        }
    }

    private UIElement MakeToolRow(ToolMode mode, string nameKey, string descKey)
    {
        var row = new Grid { Margin = new Thickness(13, 0, 0, 9) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(8),
            Background = Brush("#F1F5F9"), Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new Viewbox { Width = 18, Height = 18, Child = ShapeIcons.GetIcon(mode, HeadingBrush) }
        };
        Grid.SetColumn(badge, 0);

        var text = new StackPanel();
        Grid.SetColumn(text, 1);
        text.Children.Add(new TextBlock { Text = L10n.T(nameKey), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = HeadingBrush });
        text.Children.Add(new TextBlock { Text = L10n.T(descKey), FontSize = 12.5, Foreground = BodyBrush, TextWrapping = TextWrapping.Wrap, LineHeight = 18 });

        row.Children.Add(badge); row.Children.Add(text);
        return row;
    }

    private UIElement MakeShapeGallery()
    {
        var wrap = new WrapPanel { Margin = new Thickness(13, 4, 0, 6), MaxWidth = 632 };
        var fill = Brush("#FFFFFF");
        var stroke = Brush("#334155");

        foreach (var (kind, nameKey, w, h) in Shapes)
        {
            // Rich-text shapes render their content from RTF/text rather than an external
            // label, so give the gallery preview a little sample to show.
            var (body, _) = kind == ShapeKind.RichText
                ? ShapeFactory.BuildBody(kind, w, h, fill, stroke, null, null, null, "Aa Bb")
                : ShapeFactory.BuildBody(kind, w, h, fill, stroke);
            if (body is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }

            var holder = new Grid { Width = 104, Height = 64 };
            holder.Children.Add(body);

            var card = new StackPanel { Width = 104, Margin = new Thickness(0, 0, 8, 8) };
            card.Children.Add(new Border
            {
                Child = holder, Background = Brush("#F8FAFC"), BorderBrush = Brush("#E2E8F0"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8)
            });
            card.Children.Add(new TextBlock
            {
                Text = L10n.T(nameKey), FontSize = 11, Foreground = BodyBrush,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
            });
            wrap.Children.Add(card);
        }
        return wrap;
    }

    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
}

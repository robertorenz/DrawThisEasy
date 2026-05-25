using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

public partial class ManualWindow : Window
{
    // A manual section is a heading plus an ordered list of blocks.
    private abstract record Block;
    private sealed record Para(string Key) : Block;
    private sealed record Bullets(string[] Keys) : Block;
    private sealed record Section(string TitleKey, Block[] Blocks);

    private static readonly Brush HeadingBrush = Brush("#0F172A");
    private static readonly Brush BodyBrush    = Brush("#334155");
    private static readonly Brush AccentBrush  = Brush("#0EA5E9");

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

    private static readonly Section[] Sections =
    {
        new("manual.overview.h", new Block[]
        {
            new Para("manual.overview.p"),
        }),
        new("manual.workspace.h", new Block[]
        {
            new Para("manual.workspace.p"),
            new Bullets(new[]
            {
                "manual.workspace.b.strip",
                "manual.workspace.b.palette",
                "manual.workspace.b.canvas",
                "manual.workspace.b.inspector",
                "manual.workspace.b.status",
            }),
        }),
        new("manual.shapes.h", new Block[]
        {
            new Para("manual.shapes.p"),
            new Para("manual.shapes.p2"),
        }),
        new("manual.connect.h", new Block[]
        {
            new Para("manual.connect.p"),
            new Para("manual.connect.p2"),
        }),
        new("manual.select.h", new Block[]
        {
            new Bullets(new[]
            {
                "manual.select.b.click",
                "manual.select.b.multi",
                "manual.select.b.marquee",
                "manual.select.b.pan",
                "manual.select.b.resize",
            }),
        }),
        new("manual.labels.h", new Block[]
        {
            new Para("manual.labels.p"),
        }),
        new("manual.colors.h", new Block[]
        {
            new Para("manual.colors.p"),
        }),
        new("manual.layers.h", new Block[]
        {
            new Para("manual.layers.p"),
        }),
        new("manual.view.h", new Block[]
        {
            new Bullets(new[]
            {
                "manual.view.b.pan",
                "manual.view.b.zoom",
                "manual.view.b.reset",
            }),
        }),
        new("manual.templates.h", new Block[]
        {
            new Para("manual.templates.p"),
        }),
        new("manual.files.h", new Block[]
        {
            new Bullets(new[]
            {
                "manual.files.b.save",
                "manual.files.b.export",
                "manual.files.b.new",
            }),
        }),
        new("manual.editing.h", new Block[]
        {
            new Para("manual.editing.p"),
        }),
        new("manual.language.h", new Block[]
        {
            new Para("manual.language.p"),
        }),
        new("manual.shortcuts.h", new Block[]
        {
            new Para("manual.shortcuts.p"),
        }),
    };

    private void Build()
    {
        bool first = true;
        foreach (var section in Sections)
        {
            ManualStack.Children.Add(MakeHeading(L10n.T(section.TitleKey), first));
            first = false;
            foreach (var block in section.Blocks)
            {
                switch (block)
                {
                    case Para p:
                        ManualStack.Children.Add(MakeParagraph(L10n.T(p.Key)));
                        break;
                    case Bullets b:
                        foreach (var key in b.Keys)
                            ManualStack.Children.Add(MakeBullet(L10n.T(key)));
                        break;
                }
            }
        }
    }

    private static UIElement MakeHeading(string text, bool first)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, first ? 0 : 20, 0, 8),
        };
        panel.Children.Add(new Border
        {
            Width = 3,
            Background = AccentBrush,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 1, 10, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = HeadingBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return panel;
    }

    private static UIElement MakeParagraph(string text) => new TextBlock
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 13,
        Foreground = BodyBrush,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 19,
        Margin = new Thickness(13, 0, 0, 9),
    };

    private static UIElement MakeBullet(string text)
    {
        var row = new Grid { Margin = new Thickness(13, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock
        {
            Text = "•",
            FontSize = 14,
            Foreground = AccentBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetColumn(dot, 0);

        var body = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = BodyBrush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
        };
        Grid.SetColumn(body, 1);

        row.Children.Add(dot);
        row.Children.Add(body);
        return row;
    }

    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
}

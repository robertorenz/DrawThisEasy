using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PictureThis.Dialogs;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Build();
    }

    public static void Show(Window owner)
    {
        var w = new HelpWindow { Owner = owner };
        w.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Build()
    {
        ToolsList.Content = MakeList(new (string, string)[]
        {
            ("V", "Select tool"),
            ("L", "Connector tool"),
            ("R", "Rectangle"),
            ("O", "Rounded / Component"),
            ("E", "Ellipse"),
            ("D", "Diamond / Decision"),
            ("H", "Hexagon"),
            ("B", "Database (cylinder)"),
            ("C", "Cloud"),
            ("S", "Server"),
            ("P", "User / Person"),
            ("T", "Text label"),
            ("Esc", "Cancel / select")
        });
        EditList.Content = MakeList(new (string, string)[]
        {
            ("Ctrl+Z", "Undo"),
            ("Ctrl+Y", "Redo"),
            ("Ctrl+C", "Copy"),
            ("Ctrl+X", "Cut"),
            ("Ctrl+V", "Paste"),
            ("Ctrl+D", "Duplicate"),
            ("Ctrl+A", "Select all"),
            ("Del", "Delete selection"),
            ("Double-click", "Edit label")
        });
        FileList.Content = MakeList(new (string, string)[]
        {
            ("Ctrl+N", "New diagram"),
            ("Ctrl+O", "Open"),
            ("Ctrl+S", "Save"),
            ("Ctrl+E", "Export PNG")
        });
        ViewList.Content = MakeList(new (string, string)[]
        {
            ("Space + drag", "Pan canvas"),
            ("Right-click + drag", "Pan canvas"),
            ("Wheel", "Zoom in / out")
        });
    }

    private static StackPanel MakeList((string key, string label)[] items)
    {
        var sp = new StackPanel();
        foreach (var (key, label) in items)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var kbd = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString("#F1F5F9")!,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#CBD5E1")!,
                BorderThickness = new Thickness(1, 1, 1, 2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = key,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = (Brush)new BrushConverter().ConvertFromString("#0F172A")!
                },
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(kbd, Dock.Right);
            row.Children.Add(kbd);
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (Brush)new BrushConverter().ConvertFromString("#0F172A")!,
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(row);
        }
        return sp;
    }
}

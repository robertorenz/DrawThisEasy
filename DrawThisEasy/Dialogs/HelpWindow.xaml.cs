using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        TitleText.Text  = L10n.T("help.title");
        LblTools.Text   = L10n.T("help.tools");
        LblEditing.Text = L10n.T("help.editing");
        LblFile.Text    = L10n.T("help.file");
        LblView.Text    = L10n.T("help.view");
        BtnClose.Content = L10n.T("help.close");
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
            ("V",   L10n.T("help.action.select")),
            ("L",   L10n.T("help.action.connector")),
            ("R",   L10n.T("help.action.rectangle")),
            ("O",   L10n.T("help.action.rounded")),
            ("E",   L10n.T("help.action.ellipse")),
            ("D",   L10n.T("help.action.diamond")),
            ("H",   L10n.T("help.action.hexagon")),
            ("B",   L10n.T("help.action.database")),
            ("C",   L10n.T("help.action.cloud")),
            ("S",   L10n.T("help.action.server")),
            ("P",   L10n.T("help.action.user")),
            ("T",   L10n.T("help.action.text")),
            ("Esc", L10n.T("help.action.cancel"))
        });
        EditList.Content = MakeList(new (string, string)[]
        {
            ("Ctrl+Z", L10n.T("help.action.undo")),
            ("Ctrl+Y", L10n.T("help.action.redo")),
            ("Ctrl+C", L10n.T("help.action.copy")),
            ("Ctrl+X", L10n.T("help.action.cut")),
            ("Ctrl+V", L10n.T("help.action.paste")),
            ("Ctrl+D", L10n.T("help.action.duplicate")),
            ("Ctrl+A", L10n.T("help.action.selectall")),
            ("Del",    L10n.T("help.action.delete")),
            (L10n.T("help.action.doubleclick"), L10n.T("help.action.edit"))
        });
        FileList.Content = MakeList(new (string, string)[]
        {
            ("Ctrl+N", L10n.T("help.action.new")),
            ("Ctrl+O", L10n.T("help.action.open")),
            ("Ctrl+S", L10n.T("help.action.save")),
            ("Ctrl+E", L10n.T("help.action.export"))
        });
        ViewList.Content = MakeList(new (string, string)[]
        {
            ("Wheel",        L10n.T("help.action.scroll")),
            ("Ctrl + Wheel", L10n.T("help.action.zoom")),
            ("Space + drag", L10n.T("help.action.pan.space"))
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

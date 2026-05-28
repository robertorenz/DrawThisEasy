using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

/// Dialog that returns a new Favorites toolbar selection plus visibility toggles.
/// Items are reorderable via the up/down arrows next to each row.
public partial class CustomizeToolbarWindow : Window
{
    public List<string> Selected { get; private set; } = new();
    public bool ShowMain { get; private set; }
    public bool ShowFavorites { get; private set; }

    // Per-row state we re-emit when the user clicks Save. Order in this list
    // is the order the buttons will appear on the strip.
    private sealed class Row
    {
        public string Id = "";
        public CheckBox Check = null!;
        public Border Container = null!;
    }
    private readonly List<Row> _rows = new();

    public CustomizeToolbarWindow()
    {
        InitializeComponent();

        TitleText.Text       = L10n.T("customize.title");
        SubtitleText.Text    = L10n.T("customize.subtitle");
        LblToolbars.Text     = L10n.T("customize.toolbars").ToUpperInvariant();
        ChkShowMain.Content  = L10n.T("customize.show.main");
        ChkShowFavorites.Content = L10n.T("customize.show.favorites");
        LblItems.Text        = L10n.T("customize.items").ToUpperInvariant();
        LblItemsHint.Text    = L10n.T("customize.items.hint");
        BtnReset.Content     = L10n.T("customize.reset");
        BtnCancel.Content    = L10n.T("modal.cancel");
        BtnSave.Content      = L10n.T("topbar.save");

        var s = AppSettings.Current;
        ChkShowMain.IsChecked      = s.ShowMainToolbar;
        ChkShowFavorites.IsChecked = s.ShowFavoritesToolbar;

        // Show currently pinned items first (in their saved order), then the rest
        // unchecked. This way users can re-order the active set without scrolling.
        var pinned = s.FavoriteToolbarItems.Where(MainWindow.AllFavoriteIds.Contains).ToList();
        var others = MainWindow.AllFavoriteIds.Where(id => !pinned.Contains(id));
        foreach (var id in pinned) AddRow(id, isChecked: true);
        foreach (var id in others) AddRow(id, isChecked: false);
    }

    private void AddRow(string id, bool isChecked)
    {
        var check = new CheckBox
        {
            Content = MainWindow.FavoriteLabel(id),
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextBrush")
        };

        var up = new Button { Content = "▲", Width = 26, Height = 22, Padding = new Thickness(0), FontSize = 10, Style = (Style)FindResource("IconButton") };
        var down = new Button { Content = "▼", Width = 26, Height = 22, Padding = new Thickness(0), FontSize = 10, Style = (Style)FindResource("IconButton") };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(check, 0);
        Grid.SetColumn(up, 1);
        Grid.SetColumn(down, 2);
        grid.Children.Add(check); grid.Children.Add(up); grid.Children.Add(down);

        var container = new Border
        {
            Child = grid,
            Padding = new Thickness(8, 5, 8, 5),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
        };
        container.MouseEnter += (_, _) => container.Background = (Brush)new BrushConverter().ConvertFromString("#FFF8FAFC")!;
        container.MouseLeave += (_, _) => container.Background = Brushes.Transparent;

        var row = new Row { Id = id, Check = check, Container = container };
        up.Click += (_, _) => MoveRow(row, -1);
        down.Click += (_, _) => MoveRow(row, +1);
        _rows.Add(row);
        ItemsHost.Children.Add(container);
    }

    private void MoveRow(Row row, int delta)
    {
        int i = _rows.IndexOf(row);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= _rows.Count) return;
        _rows.RemoveAt(i); _rows.Insert(j, row);
        ItemsHost.Children.RemoveAt(i); ItemsHost.Children.Insert(j, row.Container);
    }

    public static (bool ok, List<string> favorites, bool showMain, bool showFav) Show(Window owner)
    {
        var w = new CustomizeToolbarWindow { Owner = owner };
        bool ok = w.ShowDialog() == true;
        return (ok, w.Selected, w.ShowMain, w.ShowFavorites);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Selected = _rows.Where(r => r.Check.IsChecked == true).Select(r => r.Id).ToList();
        ShowMain = ChkShowMain.IsChecked == true;
        ShowFavorites = ChkShowFavorites.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        // "Reset to defaults" emits an empty favorites list, favorites hidden,
        // and the main toolbar visible. We commit it directly so the user can
        // see the canvas chrome snap back without first hitting Save.
        Selected = new List<string>();
        ShowMain = true;
        ShowFavorites = false;
        DialogResult = true;
        Close();
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DrawThisEasy.Controls;
using DrawThisEasy.Models;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

public partial class CloudServiceGalleryWindow : Window
{
    public StencilDef? SelectedStencil { get; private set; }

    public CloudServiceGalleryWindow()
    {
        InitializeComponent();
        TitleText.Text    = L10n.T("cloud.title");
        SubtitleText.Text = L10n.T("cloud.subtitle");
        BtnCancel.Content = L10n.T("templates.cancel");

        foreach (var provider in Stencils.Providers)
        {
            ProvidersStack.Children.Add(BuildProviderHeader(provider));
            var grid = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 6) };
            foreach (var def in Stencils.ForProvider(provider))
                grid.Children.Add(BuildCard(def));
            ProvidersStack.Children.Add(grid);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private UIElement BuildProviderHeader(string provider)
    {
        var accent = ProviderColor(provider);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 14, 0, 8) };
        row.Children.Add(new Border
        {
            Width = 10, Height = 10, CornerRadius = new CornerRadius(3),
            Background = accent, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = provider,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private Border BuildCard(StencilDef def)
    {
        var meta = new TextBlock
        {
            Text = def.Name,
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(4, 8, 4, 10)
        };

        var body = new StackPanel();
        var previewBorder = new Border
        {
            Background = (Brush)FindResource("CanvasBgBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 96,
            Child = BuildTilePreview(def),
            ClipToBounds = true
        };
        body.Children.Add(previewBorder);
        body.Children.Add(meta);

        var card = new Border
        {
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 10),
            Cursor = Cursors.Hand,
            Child = body
        };
        card.MouseLeftButtonDown += (s, e) =>
        {
            SelectedStencil = def;
            DialogResult = true;
            Close();
        };
        card.MouseEnter += (s, e) => card.BorderBrush = ProviderColor(def.Provider);
        card.MouseLeave += (s, e) => card.BorderBrush = (Brush)FindResource("BorderBrush");
        return card;
    }

    private static UIElement BuildTilePreview(StencilDef def)
    {
        var stroke = (Brush)new BrushConverter().ConvertFromString(def.Color)!;
        const double w = 92, h = 74;
        var (body, _) = ShapeFactory.BuildBody(ShapeKind.ServiceTile, w, h, Brushes.White, stroke, def.Id);
        if (body is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Center;
            fe.VerticalAlignment = VerticalAlignment.Center;
        }
        var grid = new Grid();
        grid.Children.Add(body);
        return grid;
    }

    private static Brush ProviderColor(string provider) => (Brush)new BrushConverter().ConvertFromString(
        provider switch
        {
            Stencils.Aws   => Stencils.AwsColor,
            Stencils.Azure => Stencils.AzureColor,
            Stencils.Gcp   => Stencils.GcpColor,
            _              => "#334155"
        })!;
}

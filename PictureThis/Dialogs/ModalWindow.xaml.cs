using System;
using System.Windows;
using System.Windows.Controls;

namespace PictureThis.Dialogs;

public partial class ModalWindow : Window
{
    public ModalWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public static void Info(Window owner, string title, string body)
    {
        var w = new ModalWindow { Owner = owner };
        w.TitleText.Text = title;
        w.BodyText.Text = body;
        w.AddButton("OK", primary: true, click: () => w.Close());
        w.ShowDialog();
    }

    public static void Confirm(Window owner, string title, string body, string confirmLabel, Action onConfirm)
    {
        var w = new ModalWindow { Owner = owner };
        w.TitleText.Text = title;
        w.BodyText.Text = body;
        w.AddButton("Cancel", primary: false, click: () => w.Close());
        w.AddButton(confirmLabel, primary: true, click: () =>
        {
            w.Close();
            onConfirm();
        });
        w.ShowDialog();
    }

    private void AddButton(string label, bool primary, Action click)
    {
        var styleKey = primary ? "PrimaryButton" : "GhostButton";
        var btn = new Button
        {
            Content = label,
            Style = (Style)FindResource(styleKey),
            MinWidth = 92,
            Margin = new Thickness(6, 0, 0, 0)
        };
        if (!primary)
        {
            btn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            btn.BorderThickness = new Thickness(1);
        }
        btn.Click += (s, e) => click();
        ButtonBar.Children.Add(btn);
    }
}

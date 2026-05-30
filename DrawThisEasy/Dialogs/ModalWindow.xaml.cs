using System;
using System.Windows;
using System.Windows.Controls;

namespace DrawThisEasy.Dialogs;

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
        w.AddButton(DrawThisEasy.Services.L10n.T("modal.ok"), primary: true, click: () => w.Close());
        w.ShowDialog();
    }

    public static void Confirm(Window owner, string title, string body, string confirmLabel, Action onConfirm)
    {
        var w = new ModalWindow { Owner = owner };
        w.TitleText.Text = title;
        w.BodyText.Text = body;
        w.AddButton(DrawThisEasy.Services.L10n.T("modal.cancel"), primary: false, click: () => w.Close());
        w.AddButton(confirmLabel, primary: true, click: () =>
        {
            w.Close();
            onConfirm();
        });
        w.ShowDialog();
    }

    public enum UnsavedChoice { Save, Discard, Cancel }

    /// Three-way prompt: Save / Don't save / Cancel. Returns the user's choice
    /// (closing the dialog via the ✕ or Esc is treated as Cancel).
    public static UnsavedChoice AskSaveBeforeClosing(
        Window owner, string title, string body,
        string saveLabel, string discardLabel, string cancelLabel)
    {
        var w = new ModalWindow { Owner = owner };
        w.TitleText.Text = title;
        w.BodyText.Text = body;
        var choice = UnsavedChoice.Cancel;
        w.AddButton(cancelLabel,  primary: false, click: () => { choice = UnsavedChoice.Cancel;  w.Close(); });
        w.AddButton(discardLabel, primary: false, click: () => { choice = UnsavedChoice.Discard; w.Close(); });
        w.AddButton(saveLabel,    primary: true,  click: () => { choice = UnsavedChoice.Save;    w.Close(); });
        w.ShowDialog();
        return choice;
    }

    public enum PlacementChoice { Current, NewDoc, Cancel }

    /// Three-way prompt for where to put a template: current document / new document / cancel.
    public static PlacementChoice AskPlacement(
        Window owner, string title, string body,
        string currentLabel, string newLabel, string cancelLabel)
    {
        var w = new ModalWindow { Owner = owner };
        w.TitleText.Text = title;
        w.BodyText.Text = body;
        var choice = PlacementChoice.Cancel;
        w.AddButton(cancelLabel,  primary: false, click: () => { choice = PlacementChoice.Cancel;  w.Close(); });
        w.AddButton(newLabel,     primary: false, click: () => { choice = PlacementChoice.NewDoc;  w.Close(); });
        w.AddButton(currentLabel, primary: true,  click: () => { choice = PlacementChoice.Current; w.Close(); });
        w.ShowDialog();
        return choice;
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

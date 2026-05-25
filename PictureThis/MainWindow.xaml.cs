using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using PictureThis.Controls;
using PictureThis.Models;
using PictureThis.Services;
using PictureThis.Dialogs;
using IOPath = System.IO.Path;

namespace PictureThis;

public partial class MainWindow : Window
{
    // Multiple ToggleButtons can represent the same tool (palette + top strip).
    private readonly Dictionary<ToolMode, List<ToggleButton>> _toolButtons = new();

    public MainWindow()
    {
        InitializeComponent();
        BuildToolStrip();
        BuildPalette();
        BuildSwatches();

        Diagram.ToolChanged += (s, mode) => SyncToolButtons();
        Diagram.SelectionChanged += (s, e) => UpdateInspectorVisibility();
        Diagram.ZoomChanged += (s, e) => UpdateZoomLabel();
        Diagram.ModelDirty += (s, e) => { /* placeholder for unsaved indicator */ };

        SyncToolButtons();
        UpdateInspectorVisibility();
        UpdateZoomLabel();
    }

    // ===== Palette =====

    private record ToolDef(ToolMode Mode, string Label, string Title);

    private void BuildPalette()
    {
        AddGroup("Tools", new[]
        {
            new ToolDef(ToolMode.Select,  "Select",    "Select & move (V)"),
            new ToolDef(ToolMode.Connect, "Connector", "Connect shapes (L)"),
            new ToolDef(ToolMode.Pan,     "Pan",       "Pan canvas (Space + drag)")
        });

        AddGroup("Shapes", new[]
        {
            new ToolDef(ToolMode.AddRectangle,     "Process",      "Rectangle (R)"),
            new ToolDef(ToolMode.AddRounded,       "Component",    "Rounded rectangle (O)"),
            new ToolDef(ToolMode.AddEllipse,       "Start / End",  "Ellipse (E)"),
            new ToolDef(ToolMode.AddDiamond,       "Decision",     "Diamond (D)"),
            new ToolDef(ToolMode.AddHexagon,       "Hexagon",      "Hexagon (H)"),
            new ToolDef(ToolMode.AddParallelogram, "Data",         "Parallelogram"),
        });

        AddGroup("Infrastructure", new[]
        {
            new ToolDef(ToolMode.AddCylinder, "Database",     "Cylinder / Database (B)"),
            new ToolDef(ToolMode.AddCloud,    "Cloud",        "Cloud service (C)"),
            new ToolDef(ToolMode.AddServer,   "Server",       "Server (S)"),
            new ToolDef(ToolMode.AddPerson,   "User",         "Person (P)"),
            new ToolDef(ToolMode.AddQueue,    "Queue",        "Queue / stream"),
            new ToolDef(ToolMode.AddNote,     "Note",         "Sticky note"),
            new ToolDef(ToolMode.AddText,     "Text",         "Text label (T)"),
        });

        var hint = new TextBlock
        {
            Text = "Click a tool, then click the canvas.\nDrag from a shape's edge in Connector mode to link two shapes.",
            FontSize = 11,
            Foreground = (Brush)FindResource("SidebarMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6, 16, 6, 0)
        };
        PaletteStack.Children.Add(hint);
    }

    private void AddGroup(string title, ToolDef[] tools)
    {
        var label = new TextBlock
        {
            Text = title.ToUpper(),
            Style = (Style)FindResource("GroupLabel")
        };
        PaletteStack.Children.Add(label);
        foreach (var t in tools) AddToolButton(t);

        // Subtle separator
        PaletteStack.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Height = 1, Margin = new Thickness(0, 8, 0, 6)
        });
    }

    private void AddToolButton(ToolDef def)
    {
        var icon = ShapeIcons.GetIcon(def.Mode, (Brush)FindResource("SidebarTextBrush"));
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Border
        {
            Child = icon,
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0)
        });
        content.Children.Add(new TextBlock { Text = def.Label, VerticalAlignment = VerticalAlignment.Center });

        var btn = new ToggleButton
        {
            Style = (Style)FindResource("ToolButton"),
            Content = content,
            ToolTip = def.Title,
            Tag = def.Mode,
        };
        btn.Checked += (s, e) => Diagram.CurrentTool = (ToolMode)btn.Tag;
        btn.Click += (s, e) =>
        {
            // Disallow unchecking by re-click
            if (btn.IsChecked != true) btn.IsChecked = true;
        };
        RegisterToolButton(def.Mode, btn);
        PaletteStack.Children.Add(btn);
    }

    private void RegisterToolButton(ToolMode mode, ToggleButton btn)
    {
        if (!_toolButtons.TryGetValue(mode, out var list))
        {
            list = new List<ToggleButton>();
            _toolButtons[mode] = list;
        }
        list.Add(btn);
    }

    // ----- Top tool strip (compact icon row) -----

    private void BuildToolStrip()
    {
        ToolMode[][] groups =
        {
            new[] { ToolMode.Select, ToolMode.Connect, ToolMode.Pan },
            new[] { ToolMode.AddRectangle, ToolMode.AddRounded, ToolMode.AddEllipse,
                    ToolMode.AddDiamond,   ToolMode.AddHexagon, ToolMode.AddParallelogram },
            new[] { ToolMode.AddCylinder, ToolMode.AddCloud, ToolMode.AddServer,
                    ToolMode.AddPerson,   ToolMode.AddQueue, ToolMode.AddNote, ToolMode.AddText },
        };

        for (int g = 0; g < groups.Length; g++)
        {
            foreach (var mode in groups[g]) ToolStrip.Children.Add(BuildStripButton(mode));
            if (g < groups.Length - 1) ToolStrip.Children.Add(BuildStripSeparator());
        }
    }

    private ToggleButton BuildStripButton(ToolMode mode)
    {
        var btn = new ToggleButton
        {
            Style = (Style)FindResource("StripToolButton"),
            Tag = mode,
            ToolTip = StripTooltip(mode),
            Content = ShapeIcons.GetIcon(mode, (Brush)FindResource("TextMutedBrush"))
        };
        btn.Checked += (s, e) => Diagram.CurrentTool = (ToolMode)btn.Tag;
        btn.Click += (s, e) =>
        {
            if (btn.IsChecked != true) btn.IsChecked = true;
        };
        RegisterToolButton(mode, btn);
        return btn;
    }

    private static UIElement BuildStripSeparator()
    {
        return new Border
        {
            Width = 1, Height = 20,
            Margin = new Thickness(6, 0, 6, 0),
            Background = (Brush)new BrushConverter().ConvertFromString("#FFE2E8F0")!,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static string StripTooltip(ToolMode mode) => mode switch
    {
        ToolMode.Select               => "Select (V)",
        ToolMode.Connect              => "Connector (L)",
        ToolMode.Pan                  => "Pan (Space + drag)",
        ToolMode.AddRectangle         => "Rectangle / Process (R)",
        ToolMode.AddRounded           => "Rounded / Component (O)",
        ToolMode.AddEllipse           => "Ellipse / Start-End (E)",
        ToolMode.AddDiamond           => "Decision (D)",
        ToolMode.AddHexagon           => "Hexagon (H)",
        ToolMode.AddParallelogram     => "Data",
        ToolMode.AddCylinder          => "Database (B)",
        ToolMode.AddCloud             => "Cloud (C)",
        ToolMode.AddServer            => "Server (S)",
        ToolMode.AddPerson            => "User / Person (P)",
        ToolMode.AddQueue             => "Queue",
        ToolMode.AddNote              => "Sticky note",
        ToolMode.AddText              => "Text (T)",
        _ => mode.ToString()
    };

    private void SyncToolButtons()
    {
        foreach (var (mode, list) in _toolButtons)
            foreach (var btn in list)
                btn.IsChecked = (mode == Diagram.CurrentTool);

        StatusTool.Text = ToolDisplayName(Diagram.CurrentTool);
        StatusHint.Text = HintForTool(Diagram.CurrentTool);
    }

    private static string ToolDisplayName(ToolMode m) => m switch
    {
        ToolMode.Select => "Select",
        ToolMode.Connect => "Connector",
        ToolMode.Pan => "Pan",
        ToolMode.AddRectangle => "Add Rectangle",
        ToolMode.AddRounded => "Add Component",
        ToolMode.AddEllipse => "Add Ellipse",
        ToolMode.AddDiamond => "Add Decision",
        ToolMode.AddHexagon => "Add Hexagon",
        ToolMode.AddParallelogram => "Add Data",
        ToolMode.AddCylinder => "Add Database",
        ToolMode.AddCloud => "Add Cloud",
        ToolMode.AddServer => "Add Server",
        ToolMode.AddPerson => "Add User",
        ToolMode.AddQueue => "Add Queue",
        ToolMode.AddNote => "Add Note",
        ToolMode.AddText => "Add Text",
        _ => m.ToString()
    };

    private static string HintForTool(ToolMode m) => m switch
    {
        ToolMode.Select => "Click to select. Drag to move. Double-click to edit text. Space + drag to pan.",
        ToolMode.Connect => "Click a shape, drag to another shape to connect them.",
        ToolMode.Pan => "Drag the canvas to pan.",
        _ => "Click the canvas to drop the shape."
    };

    // ===== Swatches =====

    private void BuildSwatches()
    {
        foreach (var hex in Palette.Fills)
            FillSwatches.Children.Add(BuildSwatch(hex, isFill: true));
        foreach (var hex in Palette.Strokes)
            StrokeSwatches.Children.Add(BuildSwatch(hex, isFill: false));
    }

    private Border BuildSwatch(string hex, bool isFill)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        var border = new Border
        {
            Width = 22, Height = 22,
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(6),
            Background = brush,
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = hex
        };
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (isFill) Diagram.SetSelectedFill(hex);
            else Diagram.SetSelectedStroke(hex);
        };
        return border;
    }

    // ===== Inspector =====

    private void UpdateInspectorVisibility()
    {
        Inspector.Visibility = Diagram.SelectedShapeIds.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnFront_Click(object sender, RoutedEventArgs e) => Diagram.BringToFront();
    private void BtnBack_Click(object sender, RoutedEventArgs e) => Diagram.SendToBack();
    private void BtnDup_Click(object sender, RoutedEventArgs e) => Diagram.DuplicateSelection();
    private void BtnDel_Click(object sender, RoutedEventArgs e) => Diagram.DeleteSelection();

    // ===== Top bar =====

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        ModalWindow.Confirm(this, "New diagram?", "This will clear the current canvas. You'll lose any unsaved changes.",
            confirmLabel: "Start new",
            onConfirm: () => Diagram.NewDiagram());
    }

    private void BtnTemplates_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TemplateGalleryWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedTemplate != null)
            Diagram.LoadModel(CloneModel(dlg.SelectedTemplate.Builder));
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PictureThis JSON (*.ptd.json;*.json)|*.ptd.json;*.json|All files (*.*)|*.*",
            Title = "Open diagram"
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                var model = Persistence.Load(dlg.FileName);
                Diagram.LoadModel(model);
            }
            catch (Exception ex)
            {
                ModalWindow.Info(this, "Could not open file", ex.Message);
            }
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PictureThis JSON (*.ptd.json)|*.ptd.json|JSON (*.json)|*.json",
            FileName = SanitizeFilename(Diagram.Model.Title) + ".ptd.json",
            Title = "Save diagram"
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                Persistence.Save(Diagram.Model, dlg.FileName);
                ModalWindow.Info(this, "Saved", $"Diagram saved to {IOPath.GetFileName(dlg.FileName)}.");
            }
            catch (Exception ex)
            {
                ModalWindow.Info(this, "Could not save", ex.Message);
            }
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = SanitizeFilename(Diagram.Model.Title) + ".png",
            Title = "Export as PNG"
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                // Force layout so the canvas's ActualWidth/Height are current
                Diagram.UpdateLayout();
                Exporter.ExportPng(Diagram, dlg.FileName, scale: 2.0,
                    background: (Brush)FindResource("CanvasBgBrush"));
                ModalWindow.Info(this, "Exported", $"PNG written to {IOPath.GetFileName(dlg.FileName)}.");
            }
            catch (Exception ex)
            {
                ModalWindow.Info(this, "Could not export", ex.Message);
            }
        }
    }

    private static string SanitizeFilename(string s)
    {
        foreach (var c in IOPath.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "diagram" : s.Trim();
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => Diagram.SetZoom(Diagram.Zoom * 1.2);
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => Diagram.SetZoom(Diagram.Zoom / 1.2);
    private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => Diagram.ResetView();

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{(int)Math.Round(Diagram.Zoom * 100)}%";

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        HelpWindow.Show(this);
    }

    // ===== Keyboard =====

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Ignore if focus is inside a textbox (e.g. editing a label)
        if (Keyboard.FocusedElement is TextBox) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch (e.Key)
            {
                case Key.N: BtnNew_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.O: BtnOpen_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.S: BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.E: BtnExport_Click(this, new RoutedEventArgs()); e.Handled = true; return;
            }
        }

        if (e.Key == Key.OemQuestion && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            BtnHelp_Click(this, new RoutedEventArgs()); e.Handled = true; return;
        }

        Diagram.HandleKeyDown(e);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;
        Diagram.HandleKeyUp(e);
    }

    // Deep-clone a template model (so editing doesn't pollute the original)
    private static DiagramModel CloneModel(DiagramModel m)
    {
        var clone = new DiagramModel { Title = m.Title };
        var idMap = new Dictionary<string, string>();
        foreach (var s in m.Shapes)
        {
            var c = new ShapeNode
            {
                Kind = s.Kind, X = s.X, Y = s.Y, Width = s.Width, Height = s.Height,
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke, ZIndex = s.ZIndex
            };
            idMap[s.Id] = c.Id;
            clone.Shapes.Add(c);
        }
        foreach (var c in m.Connections)
        {
            if (!idMap.ContainsKey(c.FromId) || !idMap.ContainsKey(c.ToId)) continue;
            clone.Connections.Add(new Connection
            {
                FromId = idMap[c.FromId], ToId = idMap[c.ToId],
                Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed
            });
        }
        return clone;
    }
}

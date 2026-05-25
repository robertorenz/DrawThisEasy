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
using DrawThisEasy.Controls;
using DrawThisEasy.Models;
using DrawThisEasy.Services;
using DrawThisEasy.Dialogs;
using IOPath = System.IO.Path;

namespace DrawThisEasy;

public partial class MainWindow : Window
{
    // Multiple ToggleButtons can represent the same tool (palette + top strip).
    private readonly Dictionary<ToolMode, List<ToggleButton>> _toolButtons = new();
    // Refs we re-translate when language changes
    private readonly Dictionary<ToggleButton, string> _toolLabelKey = new(); // palette button -> label key
    private readonly Dictionary<ToggleButton, string> _toolTipKey   = new(); // any tool button -> tooltip key
    private readonly Dictionary<TextBlock, string>    _toolLabelTb  = new(); // palette TextBlock -> label key
    private readonly List<(TextBlock TextBlock, string Key)> _groupLabels = new();
    private TextBlock? _paletteHint;

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

        L10n.LanguageChanged += (s, e) => ApplyLanguage();
        ApplyLanguage();

        SyncToolButtons();
        UpdateInspectorVisibility();
        UpdateZoomLabel();
    }

    // ===== Palette =====

    private record ToolDef(ToolMode Mode, string LabelKey, string TipKey);

    private void BuildPalette()
    {
        AddGroup("group.tools", new[]
        {
            new ToolDef(ToolMode.Select,  "tool.select",  "tip.select"),
            new ToolDef(ToolMode.Connect, "tool.connect", "tip.connect"),
            new ToolDef(ToolMode.Pan,     "tool.pan",     "tip.pan")
        });

        AddGroup("group.shapes", new[]
        {
            new ToolDef(ToolMode.AddRectangle,     "tool.process",   "tip.rectangle"),
            new ToolDef(ToolMode.AddRounded,       "tool.component", "tip.rounded"),
            new ToolDef(ToolMode.AddEllipse,       "tool.startend",  "tip.ellipse"),
            new ToolDef(ToolMode.AddDiamond,       "tool.decision",  "tip.diamond"),
            new ToolDef(ToolMode.AddHexagon,       "tool.hexagon",   "tip.hexagon"),
            new ToolDef(ToolMode.AddParallelogram, "tool.data",      "tip.parallelogram"),
        });

        AddGroup("group.infra", new[]
        {
            new ToolDef(ToolMode.AddCylinder, "tool.database", "tip.cylinder"),
            new ToolDef(ToolMode.AddCloud,    "tool.cloud",    "tip.cloud"),
            new ToolDef(ToolMode.AddServer,   "tool.server",   "tip.server"),
            new ToolDef(ToolMode.AddPerson,   "tool.user",     "tip.person"),
            new ToolDef(ToolMode.AddQueue,    "tool.queue",    "tip.queue"),
            new ToolDef(ToolMode.AddNote,     "tool.note",     "tip.note"),
            new ToolDef(ToolMode.AddText,     "tool.text",     "tip.text"),
        });

        _paletteHint = new TextBlock
        {
            Text = L10n.T("palette.hint"),
            FontSize = 11,
            Foreground = (Brush)FindResource("SidebarMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6, 16, 6, 0)
        };
        PaletteStack.Children.Add(_paletteHint);
    }

    private void AddGroup(string titleKey, ToolDef[] tools)
    {
        var label = new TextBlock
        {
            Text = L10n.T(titleKey).ToUpper(),
            Style = (Style)FindResource("GroupLabel")
        };
        _groupLabels.Add((label, titleKey));
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
        var labelTb = new TextBlock { Text = L10n.T(def.LabelKey), VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Border
        {
            Child = icon,
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0)
        });
        content.Children.Add(labelTb);

        var btn = new ToggleButton
        {
            Style = (Style)FindResource("ToolButton"),
            Content = content,
            ToolTip = L10n.T(def.TipKey),
            Tag = def.Mode,
        };
        btn.Checked += (s, e) => Diagram.CurrentTool = (ToolMode)btn.Tag;
        btn.Click += (s, e) =>
        {
            // Disallow unchecking by re-click
            if (btn.IsChecked != true) btn.IsChecked = true;
        };
        RegisterToolButton(def.Mode, btn);
        _toolLabelKey[btn] = def.LabelKey;
        _toolTipKey[btn] = def.TipKey;
        _toolLabelTb[labelTb] = def.LabelKey;
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
        var tipKey = TipKeyForTool(mode);
        var btn = new ToggleButton
        {
            Style = (Style)FindResource("StripToolButton"),
            Tag = mode,
            ToolTip = L10n.T(tipKey),
            Content = ShapeIcons.GetIcon(mode, (Brush)FindResource("TextMutedBrush"))
        };
        btn.Checked += (s, e) => Diagram.CurrentTool = (ToolMode)btn.Tag;
        btn.Click += (s, e) =>
        {
            if (btn.IsChecked != true) btn.IsChecked = true;
        };
        RegisterToolButton(mode, btn);
        _toolTipKey[btn] = tipKey;
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

    private static string TipKeyForTool(ToolMode m) => m switch
    {
        ToolMode.Select               => "tip.select",
        ToolMode.Connect              => "tip.connect",
        ToolMode.Pan                  => "tip.pan",
        ToolMode.AddRectangle         => "tip.rectangle",
        ToolMode.AddRounded           => "tip.rounded",
        ToolMode.AddEllipse           => "tip.ellipse",
        ToolMode.AddDiamond           => "tip.diamond",
        ToolMode.AddHexagon           => "tip.hexagon",
        ToolMode.AddParallelogram     => "tip.parallelogram",
        ToolMode.AddCylinder          => "tip.cylinder",
        ToolMode.AddCloud             => "tip.cloud",
        ToolMode.AddServer            => "tip.server",
        ToolMode.AddPerson            => "tip.person",
        ToolMode.AddQueue             => "tip.queue",
        ToolMode.AddNote              => "tip.note",
        ToolMode.AddText              => "tip.text",
        _ => "tip.select"
    };

    private static string LabelKeyForTool(ToolMode m) => m switch
    {
        ToolMode.Select               => "tool.select",
        ToolMode.Connect              => "tool.connect",
        ToolMode.Pan                  => "tool.pan",
        ToolMode.AddRectangle         => "tool.process",
        ToolMode.AddRounded           => "tool.component",
        ToolMode.AddEllipse           => "tool.startend",
        ToolMode.AddDiamond           => "tool.decision",
        ToolMode.AddHexagon           => "tool.hexagon",
        ToolMode.AddParallelogram     => "tool.data",
        ToolMode.AddCylinder          => "tool.database",
        ToolMode.AddCloud             => "tool.cloud",
        ToolMode.AddServer            => "tool.server",
        ToolMode.AddPerson            => "tool.user",
        ToolMode.AddQueue             => "tool.queue",
        ToolMode.AddNote              => "tool.note",
        ToolMode.AddText              => "tool.text",
        _ => "tool.select"
    };

    private void SyncToolButtons()
    {
        foreach (var (mode, list) in _toolButtons)
            foreach (var btn in list)
                btn.IsChecked = (mode == Diagram.CurrentTool);

        StatusTool.Text = ToolDisplayName(Diagram.CurrentTool);
        StatusHint.Text = HintForTool(Diagram.CurrentTool);
    }

    private static string ToolDisplayName(ToolMode m)
    {
        var baseName = L10n.T(LabelKeyForTool(m));
        var isAdd = m != ToolMode.Select && m != ToolMode.Connect && m != ToolMode.Pan;
        return isAdd ? (L10n.T("status.add.prefix") + baseName.ToLowerInvariant()) : baseName;
    }

    private static string HintForTool(ToolMode m) => m switch
    {
        ToolMode.Select  => L10n.T("status.hint.select"),
        ToolMode.Connect => L10n.T("status.hint.connect"),
        ToolMode.Pan     => L10n.T("status.hint.pan"),
        _                => L10n.T("status.hint.add")
    };

    // ===== Swatches =====

    private void BuildSwatches()
    {
        foreach (var hex in Palette.Fills)
            FillSwatches.Children.Add(BuildSwatch(hex, isFill: true));
        FillSwatches.Children.Add(BuildMoreSwatch(isFill: true));

        foreach (var hex in Palette.Strokes)
            StrokeSwatches.Children.Add(BuildSwatch(hex, isFill: false));
        StrokeSwatches.Children.Add(BuildMoreSwatch(isFill: false));
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

    // "+" swatch that opens the custom color picker dialog.
    private Border BuildMoreSwatch(bool isFill)
    {
        // Visual: a small rainbow gradient swatch + a "+" overlay
        var border = new Border
        {
            Width = 22, Height = 22,
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(6),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = L10n.T("color.custom"),
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#F87171"), 0.0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#FBBF24"), 0.25),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#34D399"), 0.50),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#38BDF8"), 0.75),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#F472B6"), 1.0),
                },
                new Point(0, 0), new Point(1, 1))
        };
        border.Child = new TextBlock
        {
            Text = "+",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = 0.45, BlurRadius = 2, ShadowDepth = 0
            }
        };
        border.MouseLeftButtonDown += (s, e) =>
        {
            var initial = GetCurrentColorForSelection(isFill);
            var picked = ColorPickerWindow.Pick(this, initial);
            if (picked.HasValue)
            {
                var hex = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
                if (isFill) Diagram.SetSelectedFill(hex);
                else        Diagram.SetSelectedStroke(hex);
            }
        };
        return border;
    }

    private Color GetCurrentColorForSelection(bool isFill)
    {
        var shape = Diagram.GetSelectedShape();
        if (shape != null)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(isFill ? shape.Fill : shape.Stroke);
                return c;
            }
            catch { }
        }
        return isFill ? Colors.White : Color.FromRgb(0x33, 0x41, 0x55);
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
        ModalWindow.Confirm(this,
            L10n.T("modal.new.title"),
            L10n.T("modal.new.body"),
            confirmLabel: L10n.T("modal.new.confirm"),
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
            Filter = "DrawThisEasy JSON (*.ptd.json;*.json)|*.ptd.json;*.json|All files (*.*)|*.*",
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
                ModalWindow.Info(this, L10n.T("modal.openfail.title"), ex.Message);
            }
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "DrawThisEasy JSON (*.ptd.json)|*.ptd.json|JSON (*.json)|*.json",
            FileName = SanitizeFilename(Diagram.Model.Title) + ".ptd.json",
            Title = L10n.T("topbar.save")
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                Persistence.Save(Diagram.Model, dlg.FileName);
                ModalWindow.Info(this,
                    L10n.T("modal.saved.title"),
                    string.Format(L10n.T("modal.saved.body"), IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                ModalWindow.Info(this, L10n.T("modal.savefail.title"), ex.Message);
            }
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = SanitizeFilename(Diagram.Model.Title) + ".png",
            Title = L10n.T("topbar.export")
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                // Force layout so the canvas's ActualWidth/Height are current
                Diagram.UpdateLayout();
                Exporter.ExportPng(Diagram, dlg.FileName, scale: 2.0,
                    background: (Brush)FindResource("CanvasBgBrush"));
                ModalWindow.Info(this,
                    L10n.T("modal.exported.title"),
                    string.Format(L10n.T("modal.exported.body"), IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                ModalWindow.Info(this, L10n.T("modal.exportfail.title"), ex.Message);
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

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        L10n.Toggle();
    }

    // ===== Menu handlers =====

    private void MnuExit_Click(object sender, RoutedEventArgs e)      => Close();
    private void MnuUndo_Click(object sender, RoutedEventArgs e)      => Diagram.Undo();
    private void MnuRedo_Click(object sender, RoutedEventArgs e)      => Diagram.Redo();
    private void MnuCut_Click(object sender, RoutedEventArgs e)       => Diagram.Cut();
    private void MnuCopy_Click(object sender, RoutedEventArgs e)      => Diagram.Copy();
    private void MnuPaste_Click(object sender, RoutedEventArgs e)     => Diagram.Paste();
    private void MnuSelectAll_Click(object sender, RoutedEventArgs e) => Diagram.SelectAll();
    private void MnuLangEn_Click(object sender, RoutedEventArgs e)    => L10n.Current = DrawThisEasy.Services.Language.En;
    private void MnuLangEs_Click(object sender, RoutedEventArgs e)    => L10n.Current = DrawThisEasy.Services.Language.Es;

    // ===== Re-translate every string in the window =====

    private void ApplyLanguage()
    {
        // Menu headers
        MnuFile.Header           = L10n.T("menu.file");
        MnuFileNew.Header        = L10n.T("menu.file.new");
        MnuFileTemplates.Header  = L10n.T("menu.file.templates");
        MnuFileOpen.Header       = L10n.T("menu.file.open");
        MnuFileSave.Header       = L10n.T("menu.file.save");
        MnuFileExport.Header     = L10n.T("menu.file.export");
        MnuFileExit.Header       = L10n.T("menu.file.exit");

        MnuEdit.Header           = L10n.T("menu.edit");
        MnuEditUndo.Header       = L10n.T("menu.edit.undo");
        MnuEditRedo.Header       = L10n.T("menu.edit.redo");
        MnuEditCut.Header        = L10n.T("menu.edit.cut");
        MnuEditCopy.Header       = L10n.T("menu.edit.copy");
        MnuEditPaste.Header      = L10n.T("menu.edit.paste");
        MnuEditDup.Header        = L10n.T("menu.edit.duplicate");
        MnuEditDelete.Header     = L10n.T("menu.edit.delete");
        MnuEditSelectAll.Header  = L10n.T("menu.edit.selectall");

        MnuView.Header           = L10n.T("menu.view");
        MnuViewZoomIn.Header     = L10n.T("menu.view.zoomin");
        MnuViewZoomOut.Header    = L10n.T("menu.view.zoomout");
        MnuViewZoomReset.Header  = L10n.T("menu.view.zoomreset");

        MnuLang.Header           = L10n.T("menu.lang");
        MnuLangEn.Header         = L10n.T("menu.lang.en");
        MnuLangEs.Header         = L10n.T("menu.lang.es");
        MnuLangEn.IsChecked      = L10n.Current == DrawThisEasy.Services.Language.En;
        MnuLangEs.IsChecked      = L10n.Current == DrawThisEasy.Services.Language.Es;

        MnuHelp.Header           = L10n.T("menu.help");
        MnuHelpShortcuts.Header  = L10n.T("menu.help.shortcuts");

        // Zoom button tooltips
        BtnZoomOut.ToolTip   = L10n.T("topbar.zoom.out");
        BtnZoomReset.ToolTip = L10n.T("topbar.zoom.reset");
        BtnZoomIn.ToolTip    = L10n.T("topbar.zoom.in");

        // Language toggle (kept in top bar for one-click switching)
        BtnLang.Content = L10n.CurrentCode;
        BtnLang.ToolTip = L10n.T("topbar.lang.tip");
        BtnHelp.ToolTip = L10n.T("topbar.help.tip");

        // Palette group labels
        foreach (var (tb, key) in _groupLabels)
            tb.Text = L10n.T(key).ToUpper();

        // Palette tool labels
        foreach (var (tb, key) in _toolLabelTb)
            tb.Text = L10n.T(key);

        // Tooltips on tool buttons (palette + top strip)
        foreach (var (btn, key) in _toolTipKey)
            btn.ToolTip = L10n.T(key);

        // Palette footer hint
        if (_paletteHint != null) _paletteHint.Text = L10n.T("palette.hint");

        // Inspector
        LblFill.Text   = L10n.T("inspector.fill");
        LblStroke.Text = L10n.T("inspector.stroke");
        BtnFront.Content = L10n.T("inspector.front");  BtnFront.ToolTip = L10n.T("inspector.tip.front");
        BtnBack.Content  = L10n.T("inspector.back");   BtnBack.ToolTip  = L10n.T("inspector.tip.back");
        BtnDup.Content   = L10n.T("inspector.dup");    BtnDup.ToolTip   = L10n.T("inspector.tip.dup");
        BtnDel.Content   = L10n.T("inspector.delete"); BtnDel.ToolTip   = L10n.T("inspector.tip.delete");

        // Status text reflects current tool
        SyncToolButtons();
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

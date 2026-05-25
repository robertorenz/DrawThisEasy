using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    // Toolbar Templates / Insert-Image buttons (re-translated live); cloud providers use proper-noun labels.
    private Button? _tmplStripBtn;
    private TextBlock? _tmplStripLabel;
    private Button? _imgStripBtn;
    private TextBlock? _imgStripLabel;
    private TextBlock? _imgPaletteLabel;

    // ===== Open documents (tabs) =====
    private sealed class DocTab
    {
        public DiagramCanvas Canvas = null!;
        public Border Header = null!;
        public TextBlock TitleText = null!;
        public TextBlock DirtyDot = null!;
        public string Title = "Untitled";
        public bool Dirty;
    }

    private readonly List<DocTab> _docs = new();
    private DocTab _active = null!;
    private Button _addTabButton = null!;
    private int _newDocSeq;

    // All chrome (toolbar, palette, inspector, keyboard) acts on the active document's canvas.
    private DiagramCanvas Diagram => _active.Canvas;

    public MainWindow()
    {
        InitializeComponent();
        BuildToolStrip();
        BuildPalette();
        BuildSwatches();

        Closing += Window_Closing;
        L10n.LanguageChanged += (s, e) => ApplyLanguage();

        BuildAddTabButton();
        NewDocument();      // first document — wires events, activates, syncs chrome

        CanvasHost.SizeChanged += (s, e) => UpdateScrollBars();
        ApplyLanguage();
    }

    // ===== Documents (tabs) =====

    private void BuildAddTabButton()
    {
        _addTabButton = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = "+",
            FontSize = 16,
            Width = 30, Height = 26,
            Margin = new Thickness(2, 0, 0, 4),
            VerticalAlignment = VerticalAlignment.Bottom,
            ToolTip = L10n.T("topbar.new")
        };
        _addTabButton.Click += (s, e) => NewDocument();
        TabStrip.Children.Add(_addTabButton);
    }

    /// Opens a document in a new tab. With no model, a blank "Untitled N" diagram.
    private DocTab NewDocument(DiagramModel? model = null, string? title = null)
    {
        var canvas = new DiagramCanvas { Visibility = Visibility.Collapsed };
        WireCanvas(canvas);
        CanvasHost.Children.Add(canvas);

        var tab = new DocTab
        {
            Canvas = canvas,
            Title = title ?? (_newDocSeq == 0 ? "Untitled" : $"Untitled {_newDocSeq}")
        };
        _newDocSeq++;
        BuildTabHeader(tab);
        _docs.Add(tab);
        TabStrip.Children.Insert(TabStrip.Children.Count - 1, tab.Header); // before the "+"

        if (model != null) canvas.LoadModel(model);   // loading does not mark dirty
        ActivateDocument(tab);
        return tab;
    }

    private void WireCanvas(DiagramCanvas c)
    {
        c.ToolChanged          += (s, mode) => { if (ReferenceEquals(s, _active.Canvas)) SyncToolButtons(); };
        c.SelectionChanged     += (s, e)    => { if (ReferenceEquals(s, _active.Canvas)) UpdateInspectorVisibility(); };
        c.ZoomChanged          += (s, e)    => { if (ReferenceEquals(s, _active.Canvas)) UpdateZoomLabel(); };
        c.ModelDirty           += (s, e)    => { if (s is DiagramCanvas dc) MarkDirtyFor(dc); };
        c.ContextMenuRequested += (s, pt)   => { if (ReferenceEquals(s, _active.Canvas)) ShowShapeContextMenu(pt); };
        c.ViewChanged          += (s, e)    => { if (_active != null && ReferenceEquals(s, _active.Canvas)) UpdateScrollBars(); };
    }

    private void ActivateDocument(DocTab tab)
    {
        if (_active != null && _active != tab)
        {
            _active.Canvas.Visibility = Visibility.Collapsed;
            StyleTabHeader(_active, active: false);
        }
        _active = tab;
        tab.Canvas.Visibility = Visibility.Visible;
        StyleTabHeader(tab, active: true);

        // Sync all chrome to the now-active document.
        SyncToolButtons();
        UpdateInspectorVisibility();
        UpdateZoomLabel();
        UpdateWindowTitle();
        UpdateScrollBars();
        tab.Canvas.Focus();
    }

    private void CloseDocument(DocTab tab)
    {
        if (tab.Dirty)
        {
            ActivateDocument(tab);   // show what's being closed
            var choice = ModalWindow.AskSaveBeforeClosing(this,
                L10n.T("modal.unsaved.title"), L10n.T("modal.unsaved.body"),
                L10n.T("modal.unsaved.save"), L10n.T("modal.unsaved.discard"), L10n.T("modal.cancel"));
            if (choice == ModalWindow.UnsavedChoice.Cancel) return;
            if (choice == ModalWindow.UnsavedChoice.Save && !SaveDiagram(notify: false)) return;
        }

        TabStrip.Children.Remove(tab.Header);
        CanvasHost.Children.Remove(tab.Canvas);
        _docs.Remove(tab);

        if (_docs.Count == 0) { _active = null!; NewDocument(); }  // always keep one open
        else if (_active == tab) ActivateDocument(_docs[^1]);
    }

    private void BuildTabHeader(DocTab tab)
    {
        tab.TitleText = new TextBlock { Text = tab.Title, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        tab.DirtyDot = new TextBlock
        {
            Text = "•", FontSize = 14, Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("AccentBrush"),
            Visibility = Visibility.Collapsed
        };
        var close = new TextBlock
        {
            Text = "✕", FontSize = 11, Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Cursor = Cursors.Hand
        };
        close.MouseLeftButtonUp += (s, e) => { e.Handled = true; CloseDocument(tab); };

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(tab.TitleText);
        content.Children.Add(tab.DirtyDot);
        content.Children.Add(close);

        tab.Header = new Border
        {
            Child = content,
            Padding = new Thickness(12, 6, 8, 6),
            Margin = new Thickness(0, 4, 3, 0),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1, 1, 1, 0)
        };
        tab.Header.MouseLeftButtonDown += (s, e) => ActivateDocument(tab);
        StyleTabHeader(tab, active: false);
    }

    private void StyleTabHeader(DocTab tab, bool active)
    {
        tab.Header.Background = active ? Brushes.White : (Brush)new BrushConverter().ConvertFromString("#FFE2E8F0")!;
        tab.Header.BorderBrush = active ? (Brush)FindResource("BorderBrush") : Brushes.Transparent;
        tab.TitleText.Foreground = active ? (Brush)FindResource("TextBrush") : (Brush)FindResource("TextMutedBrush");
        tab.TitleText.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void UpdateTabHeader(DocTab tab)
    {
        tab.TitleText.Text = tab.Title;
        tab.DirtyDot.Visibility = tab.Dirty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWindowTitle()
    {
        var name = _active?.Title ?? "DrawThisEasy";
        Title = (_active?.Dirty == true ? "• " : "") + name + " — DrawThisEasy";
    }

    // ===== Scrollbars =====

    private void VScroll_Scroll(object sender, ScrollEventArgs e)
    {
        if (_active == null) return;
        var (x, _) = Diagram.GetScrollOffsetWorld();
        Diagram.ScrollViewTo(x, VScroll.Value);
    }

    private void HScroll_Scroll(object sender, ScrollEventArgs e)
    {
        if (_active == null) return;
        var (_, y) = Diagram.GetScrollOffsetWorld();
        Diagram.ScrollViewTo(HScroll.Value, y);
    }

    private void UpdateScrollBars()
    {
        if (_active == null) return;
        var vp = Diagram.GetViewportWorldSize();
        if (vp.Width <= 0 || vp.Height <= 0) return;

        var ext = Diagram.GetContentExtentWorld();
        var (tlX, tlY) = Diagram.GetScrollOffsetWorld();

        // Keep the current view inside the scrollable range.
        double left = Math.Min(ext.Left, tlX), top = Math.Min(ext.Top, tlY);
        double right = Math.Max(ext.Right, tlX + vp.Width), bottom = Math.Max(ext.Bottom, tlY + vp.Height);

        SetBar(VScroll, top, bottom, vp.Height, tlY);
        SetBar(HScroll, left, right, vp.Width, tlX);
    }

    private static void SetBar(ScrollBar bar, double min, double max, double viewport, double value)
    {
        bar.Minimum = min;
        bar.Maximum = Math.Max(min, max - viewport);
        bar.ViewportSize = viewport;
        bar.LargeChange = viewport * 0.9;
        bar.SmallChange = 40;
        bar.Value = Math.Min(Math.Max(value, bar.Minimum), bar.Maximum);
        bar.Visibility = (max - min) > viewport + 1 ? Visibility.Visible : Visibility.Collapsed;
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

        AddCloudGroup();
        AddInsertImageButton();

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

    // Cloud section in the palette: per-provider quick buttons that open the gallery filtered to that provider.
    private void AddCloudGroup()
    {
        var label = new TextBlock { Text = L10n.T("group.cloud").ToUpper(), Style = (Style)FindResource("GroupLabel") };
        _groupLabels.Add((label, "group.cloud"));
        PaletteStack.Children.Add(label);

        AddCloudProviderButton(Stencils.Aws,   Stencils.AwsColor);
        AddCloudProviderButton(Stencils.Azure, Stencils.AzureColor);
        AddCloudProviderButton(Stencils.Gcp,   Stencils.GcpColor);

        PaletteStack.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Height = 1, Margin = new Thickness(0, 8, 0, 6)
        });
    }

    private void AddCloudProviderButton(string provider, string colorHex)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFromString(colorHex)!,
            Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock { Text = provider, VerticalAlignment = VerticalAlignment.Center });

        var btn = new Button
        {
            Style = (Style)FindResource("PaletteActionButton"),
            Content = content,
            ToolTip = L10n.T("topbar.cloud.tip")
        };
        btn.Click += (s, e) => ShowCloudFlyout((UIElement)s!, provider, PlacementMode.Right);
        PaletteStack.Children.Add(btn);
    }

    private void AddInsertImageButton()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new Border
        {
            Child = BuildImageIcon((Brush)FindResource("SidebarTextBrush")),
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
        });
        _imgPaletteLabel = new TextBlock { Text = L10n.T("topbar.image.tip"), VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(_imgPaletteLabel);

        var btn = new Button
        {
            Style = (Style)FindResource("PaletteActionButton"),
            Content = content,
            ToolTip = L10n.T("topbar.image.tip")
        };
        btn.Click += (s, e) => BtnInsertImage_Click(s, e);
        PaletteStack.Children.Add(btn);
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

        // Labeled Templates button, then a per-provider cloud button (each opens a service flyout).
        ToolStrip.Children.Add(BuildStripSeparator());
        var (tBtn, tLbl) = MakeStripAction(BuildTemplatesIcon(), L10n.T("topbar.templates"), L10n.T("topbar.templates.tip"),
            (s, e) => BtnTemplates_Click(s, e));
        _tmplStripBtn = tBtn; _tmplStripLabel = tLbl;
        ToolStrip.Children.Add(tBtn);

        var (iBtn, iLbl) = MakeStripAction(BuildImageIcon((Brush)FindResource("TextMutedBrush")), L10n.T("topbar.image"), L10n.T("topbar.image.tip"),
            (s, e) => BtnInsertImage_Click(s, e));
        _imgStripBtn = iBtn; _imgStripLabel = iLbl;
        ToolStrip.Children.Add(iBtn);

        ToolStrip.Children.Add(BuildStripSeparator());
        ToolStrip.Children.Add(BuildProviderStripButton("AWS",    Stencils.Aws,   Stencils.AwsColor));
        ToolStrip.Children.Add(BuildProviderStripButton("Azure",  Stencils.Azure, Stencils.AzureColor));
        ToolStrip.Children.Add(BuildProviderStripButton("Google", Stencils.Gcp,   Stencils.GcpColor));
    }

    private Button BuildProviderStripButton(string label, string provider, string colorHex)
    {
        var chip = new Border
        {
            Width = 14, Height = 14, CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFromString(colorHex)!,
            VerticalAlignment = VerticalAlignment.Center
        };
        var (btn, _) = MakeStripAction(chip, label, provider,
            (s, e) => ShowCloudFlyout((UIElement)s!, provider, PlacementMode.Bottom));
        return btn;
    }

    private (Button Button, TextBlock Label) MakeStripAction(UIElement icon, string label, string tooltip, RoutedEventHandler onClick)
    {
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Border { Child = icon, Width = 18, Height = 18, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(lbl);
        var btn = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = content,
            ToolTip = tooltip,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(1, 0, 1, 0)
        };
        btn.Click += onClick;
        return (btn, lbl);
    }

    private UIElement BuildTemplatesIcon()
    {
        var brush = (Brush)FindResource("TextMutedBrush");
        var c = new Canvas { Width = 18, Height = 18 };
        void Sq(double x, double y)
        {
            var r = new System.Windows.Shapes.Rectangle { Width = 7, Height = 7, RadiusX = 1.5, RadiusY = 1.5, Fill = brush };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y); c.Children.Add(r);
        }
        Sq(1, 1); Sq(10, 1); Sq(1, 10); Sq(10, 10);
        return c;
    }

    private UIElement BuildImageIcon(Brush brush)
    {
        var c = new Canvas { Width = 18, Height = 18 };
        var frame = new System.Windows.Shapes.Rectangle
        {
            Width = 16, Height = 13, RadiusX = 2, RadiusY = 2,
            Stroke = brush, StrokeThickness = 1.6, Fill = null
        };
        Canvas.SetLeft(frame, 1); Canvas.SetTop(frame, 3); c.Children.Add(frame);

        var sun = new System.Windows.Shapes.Ellipse { Width = 3.6, Height = 3.6, Fill = brush };
        Canvas.SetLeft(sun, 4.5); Canvas.SetTop(sun, 6); c.Children.Add(sun);

        c.Children.Add(new System.Windows.Shapes.Polygon
        {
            Fill = brush,
            Points = new PointCollection { new Point(3, 15), new Point(8, 9.5), new Point(12, 15) }
        });
        return c;
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

    // ===== Right-click context menu =====

    private Popup? _ctxPopup;

    private void ShowShapeContextMenu(Point _)
    {
        CloseContextMenu();
        if (Diagram.SelectedShapeIds.Count == 0) return;

        var stack = new StackPanel { Width = 248 };

        stack.Children.Add(CtxActionRow("✎", L10n.T("ctx.edittext"), danger: false, () =>
        {
            var sh = Diagram.GetSelectedShape();
            if (sh != null) Diagram.BeginEditText(sh);
        }));

        stack.Children.Add(CtxSeparator());
        stack.Children.Add(CtxHeader(L10n.T("ctx.fill")));
        stack.Children.Add(CtxSwatchRow(Palette.Fills, isFill: true));
        stack.Children.Add(CtxHeader(L10n.T("ctx.stroke")));
        stack.Children.Add(CtxSwatchRow(Palette.Strokes, isFill: false));

        stack.Children.Add(CtxSeparator());
        stack.Children.Add(CtxActionRow("⤒", L10n.T("ctx.front"),     danger: false, () => Diagram.BringToFront()));
        stack.Children.Add(CtxActionRow("⤓", L10n.T("ctx.back"),      danger: false, () => Diagram.SendToBack()));
        stack.Children.Add(CtxActionRow("⧉", L10n.T("ctx.duplicate"), danger: false, () => Diagram.DuplicateSelection()));
        stack.Children.Add(CtxActionRow("❏", L10n.T("ctx.copy"),      danger: false, () => Diagram.Copy()));
        stack.Children.Add(CtxActionRow("✕", L10n.T("ctx.delete"),    danger: true,  () => Diagram.DeleteSelection()));

        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(10),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = stack,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString("#0F172A"),
                Opacity = 0.28, BlurRadius = 28, ShadowDepth = 5,
            },
        };

        _ctxPopup = new Popup
        {
            Child = card,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Placement = PlacementMode.Mouse,
            PlacementTarget = Diagram,
        };
        _ctxPopup.IsOpen = true;
    }

    private void CloseContextMenu()
    {
        if (_ctxPopup != null) { _ctxPopup.IsOpen = false; _ctxPopup = null; }
    }

    private UIElement CtxHeader(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)FindResource("TextMutedBrush"),
        Margin = new Thickness(8, 6, 8, 3),
    };

    private UIElement CtxSeparator() => new Border
    {
        Height = 1,
        Background = (Brush)FindResource("BorderBrush"),
        Margin = new Thickness(4, 5, 4, 5),
    };

    private UIElement CtxActionRow(string glyph, string label, bool danger, Action onClick)
    {
        var dangerBrush = (Brush)new BrushConverter().ConvertFromString("#EF4444")!;
        var glyphBrush  = danger ? dangerBrush : (Brush)FindResource("AccentBrush");
        var textBrush   = danger ? dangerBrush : (Brush)FindResource("TextBrush");
        var hoverBrush  = (Brush)new BrushConverter().ConvertFromString(danger ? "#FFFEF2F2" : "#FFF1F5F9")!;

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = glyph, FontSize = 13, Width = 22, Foreground = glyphBrush,
            VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center,
        });
        row.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center,
        });

        var b = new Border
        {
            Child = row,
            Padding = new Thickness(6, 7, 16, 7),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
        };
        b.MouseEnter += (s, e) => b.Background = hoverBrush;
        b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (s, e) => { CloseContextMenu(); onClick(); };
        return b;
    }

    private UIElement CtxSwatchRow(string[] colors, bool isFill)
    {
        var wrap = new WrapPanel { Margin = new Thickness(8, 0, 8, 4), MaxWidth = 232 };
        foreach (var hex in colors) wrap.Children.Add(CtxSwatch(hex, isFill));
        wrap.Children.Add(CtxCustomSwatch(isFill));
        return wrap;
    }

    private Border CtxSwatch(string hex, bool isFill)
    {
        var border = new Border
        {
            Width = 22, Height = 22,
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = hex,
        };
        border.MouseLeftButtonUp += (s, e) =>
        {
            CloseContextMenu();
            if (isFill) Diagram.SetSelectedFill(hex); else Diagram.SetSelectedStroke(hex);
            e.Handled = true;
        };
        return border;
    }

    private Border CtxCustomSwatch(bool isFill)
    {
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
                new Point(0, 0), new Point(1, 1)),
            Child = new TextBlock
            {
                Text = "+", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        border.MouseLeftButtonUp += (s, e) =>
        {
            CloseContextMenu();
            var initial = GetCurrentColorForSelection(isFill);
            var picked = ColorPickerWindow.Pick(this, initial);
            if (picked.HasValue)
            {
                var hex = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
                if (isFill) Diagram.SetSelectedFill(hex); else Diagram.SetSelectedStroke(hex);
            }
            e.Handled = true;
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

    private void BtnNew_Click(object sender, RoutedEventArgs e) => NewDocument();

    private void BtnTemplates_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TemplateGalleryWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedTemplate != null)
            NewDocument(CloneModel(dlg.SelectedTemplate.Builder), dlg.SelectedTemplate.Title);
    }

    private void BtnCloud_Click(object sender, RoutedEventArgs e) => OpenCloudGallery();

    private void OpenCloudGallery(string? provider = null)
    {
        var dlg = new CloudServiceGalleryWindow(provider) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedStencil is { } def)
            Diagram.AddServiceTile(def.Id, def.Name, def.Color);
    }

    // ===== Cloud service flyout (sidebar + toolbar provider buttons) =====

    private Popup? _cloudFlyout;

    private void ShowCloudFlyout(UIElement target, string provider, PlacementMode placement)
    {
        CloseCloudFlyout();

        var list = new StackPanel();
        foreach (var def in Stencils.ForProvider(provider))
            list.Children.Add(CloudFlyoutRow(def));

        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(10),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 460 },
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString("#0F172A"),
                Opacity = 0.28, BlurRadius = 28, ShadowDepth = 5
            }
        };

        _cloudFlyout = new Popup
        {
            Child = card,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Placement = placement,
            PlacementTarget = target
        };
        _cloudFlyout.IsOpen = true;
    }

    private void CloseCloudFlyout()
    {
        if (_cloudFlyout != null) { _cloudFlyout.IsOpen = false; _cloudFlyout = null; }
    }

    private UIElement CloudFlyoutRow(StencilDef def)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Width = 210 };
        row.Children.Add(new Border
        {
            Child = ShapeFactory.BuildServiceBadge(def.Id, 22, (Brush)new BrushConverter().ConvertFromString(def.Color)!),
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = def.Name, VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13, Foreground = (Brush)FindResource("TextBrush")
        });

        var b = new Border
        {
            Child = row,
            Padding = new Thickness(8, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        var hover = (Brush)new BrushConverter().ConvertFromString("#FFF1F5F9")!;
        b.MouseEnter += (s, e) => b.Background = hover;
        b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (s, e) =>
        {
            CloseCloudFlyout();
            Diagram.AddServiceTile(def.Id, def.Name, def.Color);
        };
        return b;
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Diagrams (*.ptd.json;*.json;*.excalidraw)|*.ptd.json;*.json;*.excalidraw|All files (*.*)|*.*",
            Title = "Open diagram",
            Multiselect = true   // pick several files; each opens in its own tab
        };
        if (dlg.ShowDialog(this) != true) return;
        OpenPaths(dlg.FileNames);
    }

    /// Opens each path in its own tab (native .ptd.json or Excalidraw), records it in Recent, reports errors.
    private void OpenPaths(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        foreach (var file in paths)
        {
            try
            {
                var text = File.ReadAllText(file);
                var model = IsExcalidraw(file, text) ? DiagramImport.FromExcalidraw(text) : Persistence.Load(file);
                NewDocument(model, IOPath.GetFileNameWithoutExtension(file));
                RecentFiles.Add(file);
            }
            catch (Exception ex)
            {
                errors.Add($"{IOPath.GetFileName(file)}: {ex.Message}");
            }
        }
        RebuildRecentMenu();
        if (errors.Count > 0)
            ModalWindow.Info(this, L10n.T("modal.openfail.title"), string.Join("\n", errors));
    }

    private void RebuildRecentMenu()
    {
        MnuFileRecent.Items.Clear();
        var existing = RecentFiles.Load().Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            MnuFileRecent.Items.Add(new MenuItem { Header = L10n.T("menu.file.recent.none"), IsEnabled = false });
            return;
        }
        int i = 1;
        foreach (var path in existing)
        {
            var captured = path;
            MnuFileRecent.Items.Add(new MenuItem { Header = $"_{i} {IOPath.GetFileName(path)}", ToolTip = path });
            ((MenuItem)MnuFileRecent.Items[^1]).Click += (s, e) => OpenPaths(new[] { captured });
            i++;
        }
        MnuFileRecent.Items.Add(new Separator());
        var clear = new MenuItem { Header = L10n.T("menu.file.recent.clear") };
        clear.Click += (s, e) => { RecentFiles.Clear(); RebuildRecentMenu(); };
        MnuFileRecent.Items.Add(clear);
    }

    private static bool IsExcalidraw(string path, string content)
    {
        if (IOPath.GetExtension(path).Equals(".excalidraw", StringComparison.OrdinalIgnoreCase))
            return true;
        // Detect an Excalidraw scene saved as .json by its type marker.
        return content.Contains("\"type\"", StringComparison.Ordinal)
            && content.Contains("\"excalidraw\"", StringComparison.Ordinal);
    }

    private void BtnImportExcalidraw_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excalidraw (*.excalidraw;*.json)|*.excalidraw;*.json|All files (*.*)|*.*",
            Title = L10n.T("menu.file.import.excalidraw"),
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;
        OpenPaths(dlg.FileNames);
    }

    private void BtnInsertImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*",
            Title = L10n.T("menu.edit.insertimage")
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            var ext = IOPath.GetExtension(dlg.FileName).TrimStart('.').ToLowerInvariant();
            var mime = ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "bmp" => "image/bmp",
                _ => "image/png"
            };
            var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

            // Use the image's pixel size, scaled down to a sensible default.
            double w = 200, h = 150;
            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = ms; bmp.EndInit();
                }
                if (bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    double scale = Math.Min(1.0, 320.0 / Math.Max(bmp.PixelWidth, bmp.PixelHeight));
                    w = Math.Max(24, bmp.PixelWidth * scale);
                    h = Math.Max(24, bmp.PixelHeight * scale);
                }
            }
            catch { /* keep the default size */ }

            Diagram.AddImage(dataUrl, w, h);
        }
        catch (Exception ex)
        {
            ModalWindow.Info(this, L10n.T("modal.openfail.title"), ex.Message);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveDiagram();

    /// Runs the Save dialog. Returns true only if the diagram was written to disk.
    /// Pass notify: false to skip the "Saved" confirmation (e.g. when saving on exit).
    private bool SaveDiagram(bool notify = true)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "DrawThisEasy JSON (*.ptd.json)|*.ptd.json|JSON (*.json)|*.json",
            FileName = SanitizeFilename(_active.Title) + ".ptd.json",
            Title = L10n.T("topbar.save")
        };
        if (dlg.ShowDialog(this) != true) return false; // user canceled the save dialog

        try
        {
            Persistence.Save(Diagram.Model, dlg.FileName);
            _active.Title = IOPath.GetFileNameWithoutExtension(dlg.FileName);
            MarkSaved();
            RecentFiles.Add(dlg.FileName);
            RebuildRecentMenu();
            if (notify)
                ModalWindow.Info(this,
                    L10n.T("modal.saved.title"),
                    string.Format(L10n.T("modal.saved.body"), IOPath.GetFileName(dlg.FileName)));
            return true;
        }
        catch (Exception ex)
        {
            ModalWindow.Info(this, L10n.T("modal.savefail.title"), ex.Message);
            return false;
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

    private void BtnExportExcalidraw_Click(object sender, RoutedEventArgs e) =>
        ExportText(DiagramExport.ToExcalidraw(Diagram.Model),
            "Excalidraw (*.excalidraw)|*.excalidraw|JSON (*.json)|*.json", ".excalidraw");

    private void BtnExportDrawio_Click(object sender, RoutedEventArgs e) =>
        ExportText(DiagramExport.ToDrawio(Diagram.Model),
            "draw.io (*.drawio)|*.drawio|XML (*.xml)|*.xml", ".drawio");

    private void BtnExportMermaid_Click(object sender, RoutedEventArgs e) =>
        ExportText(DiagramExport.ToMermaid(Diagram.Model),
            "Mermaid (*.mmd)|*.mmd|Text (*.txt)|*.txt", ".mmd");

    private void ExportText(string content, string filter, string defaultExt)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = SanitizeFilename(Diagram.Model.Title) + defaultExt,
            Title = L10n.T("topbar.export")
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, content);
            ModalWindow.Info(this,
                L10n.T("modal.exported.title"),
                string.Format(L10n.T("modal.exported.body"), IOPath.GetFileName(dlg.FileName)));
        }
        catch (Exception ex)
        {
            ModalWindow.Info(this, L10n.T("modal.exportfail.title"), ex.Message);
        }
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => Diagram.SetZoom(Diagram.Zoom * 1.2);
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => Diagram.SetZoom(Diagram.Zoom / 1.2);
    private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => Diagram.ResetView();

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{(int)Math.Round(Diagram.Zoom * 100)}%";

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        HelpWindow.Show(this);
    }

    private void BtnManual_Click(object sender, RoutedEventArgs e)
    {
        ManualWindow.Show(this);
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
        MnuFileCloud.Header      = L10n.T("menu.file.cloud");
        MnuFileOpen.Header       = L10n.T("menu.file.open");
        MnuFileRecent.Header     = L10n.T("menu.file.recent");
        RebuildRecentMenu();
        MnuFileImportExcalidraw.Header = L10n.T("menu.file.import.excalidraw");
        MnuFileSave.Header       = L10n.T("menu.file.save");
        MnuFileExport.Header     = L10n.T("menu.file.export");
        MnuFileExportExcalidraw.Header = L10n.T("menu.file.export.excalidraw");
        MnuFileExportDrawio.Header     = L10n.T("menu.file.export.drawio");
        MnuFileExportMermaid.Header    = L10n.T("menu.file.export.mermaid");
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
        MnuEditInsertImage.Header = L10n.T("menu.edit.insertimage");

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
        MnuHelpManual.Header     = L10n.T("menu.help.manual");
        MnuHelpShortcuts.Header  = L10n.T("menu.help.shortcuts");

        // Zoom button tooltips
        BtnZoomOut.ToolTip   = L10n.T("topbar.zoom.out");
        BtnZoomReset.ToolTip = L10n.T("topbar.zoom.reset");
        BtnZoomIn.ToolTip    = L10n.T("topbar.zoom.in");

        // Language toggle (kept in top bar for one-click switching)
        BtnLang.Content = L10n.CurrentCode;
        BtnLang.ToolTip = L10n.T("topbar.lang.tip");
        BtnHelp.ToolTip = L10n.T("topbar.help.tip");

        // Toolbar Templates button (cloud-provider buttons use proper-noun labels)
        if (_tmplStripLabel != null) _tmplStripLabel.Text = L10n.T("topbar.templates");
        if (_tmplStripBtn != null) _tmplStripBtn.ToolTip = L10n.T("topbar.templates.tip");
        if (_imgStripLabel != null) _imgStripLabel.Text = L10n.T("topbar.image");
        if (_imgStripBtn != null) _imgStripBtn.ToolTip = L10n.T("topbar.image.tip");
        if (_imgPaletteLabel != null) _imgPaletteLabel.Text = L10n.T("topbar.image.tip");

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

    // ===== Unsaved-changes tracking (per document) =====

    private void MarkDirtyFor(DiagramCanvas canvas)
    {
        var tab = _docs.Find(d => d.Canvas == canvas);
        if (tab == null || tab.Dirty) return;
        tab.Dirty = true;
        UpdateTabHeader(tab);
        if (tab == _active) UpdateWindowTitle();
    }

    private void MarkSaved()
    {
        if (_active == null) return;
        _active.Dirty = false;
        UpdateTabHeader(_active);
        UpdateWindowTitle();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Prompt for each document that has unsaved changes.
        foreach (var tab in _docs.FindAll(d => d.Dirty))
        {
            ActivateDocument(tab);
            var choice = ModalWindow.AskSaveBeforeClosing(this,
                L10n.T("modal.unsaved.title"),
                L10n.T("modal.unsaved.body"),
                saveLabel:    L10n.T("modal.unsaved.save"),
                discardLabel: L10n.T("modal.unsaved.discard"),
                cancelLabel:  L10n.T("modal.cancel"));

            if (choice == ModalWindow.UnsavedChoice.Cancel) { e.Cancel = true; return; }
            if (choice == ModalWindow.UnsavedChoice.Save && !SaveDiagram(notify: false)) { e.Cancel = true; return; }
            tab.Dirty = false;
        }
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

        if (e.Key == Key.F1)
        {
            BtnManual_Click(this, new RoutedEventArgs()); e.Handled = true; return;
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
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke, Stencil = s.Stencil, Image = s.Image, ZIndex = s.ZIndex
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawThisEasy.Models;
using DrawThisEasy.Services;

namespace DrawThisEasy.Controls;

public class DiagramCanvas : Canvas
{
    // ---- Layers ----
    private readonly Canvas _world = new();
    private readonly Canvas _connLayer = new();
    private readonly Canvas _shapeLayer = new();
    private readonly Canvas _guideLayer = new() { IsHitTestVisible = false };
    private readonly Canvas _overlayLayer = new() { IsHitTestVisible = false };
    private (bool Horizontal, double Pos)? _guidePreview;

    private readonly MatrixTransform _worldTransform = new(Matrix.Identity);
    // Applied after the world matrix (in screen space); only non-zero during the "spin" transition.
    private readonly RotateTransform _rotate = new(0);

    // ---- Model & visuals ----
    private DiagramModel _model = new();
    private readonly Dictionary<string, ShapeVisual> _shapeVisuals = new();
    private readonly Dictionary<string, ConnectionVisual> _connVisuals = new();
    private int _nextZ = 1;

    // ---- Selection ----
    private readonly HashSet<string> _selected = new();
    private string? _selectedConnectionId;

    // ---- Tool ----
    private ToolMode _tool = ToolMode.Select;

    // ---- Interaction state ----
    private enum DragMode { None, Pan, MoveShape, ResizeShape, ConnectDrag, Marquee, ConnCurve, GuideMove }
    private DragMode _drag = DragMode.None;
    private Guide? _dragGuide;
    private Point _dragStartScreen;
    private Point _dragStartWorld;
    private readonly Dictionary<string, (double X, double Y)> _dragOrigins = new();
    private Matrix _dragStartTransform;
    private string? _resizeShapeId;
    private string _resizeHandle = "";
    private (double X, double Y, double W, double H) _resizeOrigin;
    private string? _connectFromId;
    private string? _curveConnId;
    private Path? _dragLine;
    private Rectangle? _marqueeRect;
    private bool _spaceHeld;
    private bool _isEditingText;
    private bool _rightClickPending;
    private bool _rightClickConn;
    private double? _snapX, _snapY;   // active alignment-guide coordinates while moving

    /// While true (presentation mode) all mouse interaction is ignored — the view is driven
    /// only by the presentation navigation.
    public bool ReadOnly;

    // ---- Undo/redo ----
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    // ---- Clipboard ----
    private const string ClipboardFormat = "DrawThisEasy.Clipboard.v1";
    private Point _lastMouseWorld;
    private bool _hasMouseWorld;

    // ---- Events ----
    public event EventHandler? SelectionChanged;
    public event EventHandler<ToolMode>? ToolChanged;
    public event EventHandler? ZoomChanged;
    public event EventHandler? ModelDirty;
    // Raised when the user right-clicks a shape (without dragging). Arg = screen position.
    public event EventHandler<Point>? ContextMenuRequested;
    // Raised whenever the pan/zoom transform changes (so scrollbars can refresh).
    public event EventHandler? ViewChanged;
    // Raised when the user right-clicks a connector (without dragging). Arg = screen position.
    public event EventHandler<Point>? ConnectionContextRequested;

    // ---- Public API ----
    public DiagramModel Model => _model;
    public ToolMode CurrentTool
    {
        get => _tool;
        set
        {
            if (_tool == value) return;
            _tool = value;
            UpdateCursor();
            ToolChanged?.Invoke(this, value);
        }
    }

    public double Zoom => _worldTransform.Matrix.M11;
    public IReadOnlyCollection<string> SelectedShapeIds => _selected;
    public string? SelectedConnectionId => _selectedConnectionId;

    public DiagramCanvas()
    {
        Background = BuildGridBackground();
        ClipToBounds = true;
        Focusable = true;
        FocusVisualStyle = null;

        _world.RenderTransform = new TransformGroup { Children = { _worldTransform, _rotate } };
        _world.IsHitTestVisible = true;
        Children.Add(_world);
        _world.Children.Add(_connLayer);
        _world.Children.Add(_shapeLayer);
        _world.Children.Add(_guideLayer);
        _world.Children.Add(_overlayLayer);

        MouseLeftButtonDown += OnMouseLeftDown;
        MouseLeftButtonUp += OnMouseLeftUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        MouseRightButtonDown += OnMouseRightDown;
        MouseRightButtonUp += OnMouseRightUp;

        // Initial focus when added
        Loaded += (_, _) => Focus();

        // Keep guide lines spanning the visible area as the view changes / resizes.
        ViewChanged += (_, _) => { if (_model.Guides.Count > 0 || _guidePreview != null) RebuildGuides(); };
        SizeChanged += (_, _) => { if (_model.Guides.Count > 0 || _guidePreview != null) RebuildGuides(); };
    }

    // ---------- Model management ----------

    public void LoadModel(DiagramModel model)
    {
        _model = model;
        _nextZ = (model.Shapes.Count == 0 ? 0 : model.Shapes.Max(s => s.ZIndex)) + 1;
        _selected.Clear();
        _selectedConnectionId = null;
        _undoStack.Clear();
        _redoStack.Clear();
        Rebuild();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NewDiagram() => LoadModel(new DiagramModel());

    private void Rebuild()
    {
        _shapeLayer.Children.Clear();
        _connLayer.Children.Clear();
        _shapeVisuals.Clear();
        _connVisuals.Clear();

        foreach (var s in _model.Shapes.OrderBy(s => s.ZIndex))
            AddShapeVisual(s);
        foreach (var c in _model.Connections)
            AddConnectionVisual(c);
        RebuildGuides();
        RebuildOverlay();
    }

    // ---------- Ruler guides ----------

    public Point ToWorld(Point screen) => ScreenToWorld(screen);

    public void AddGuide(bool horizontal, double pos)
    {
        Snapshot();
        _model.Guides.Add(new Guide { Horizontal = horizontal, Position = pos });
        _guidePreview = null;
        RebuildGuides();
    }

    public void ClearGuides()
    {
        if (_model.Guides.Count == 0) return;
        Snapshot();
        _model.Guides.Clear();
        RebuildGuides();
    }

    public void SetGuidePreview(bool horizontal, double pos)
    {
        _guidePreview = (horizontal, pos);
        RebuildGuides();
    }

    public void ClearGuidePreview()
    {
        if (_guidePreview == null) return;
        _guidePreview = null;
        RebuildGuides();
    }

    private void RebuildGuides()
    {
        _guideLayer.Children.Clear();
        if (_model.Guides.Count == 0 && _guidePreview == null) return;

        // Span only the visible world rect (+margin); extremely long lines can fail to render
        // once the world scale transform is applied, which made guides invisible.
        var tl = ScreenToWorld(new Point(0, 0));
        var br = ScreenToWorld(new Point(Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1)));
        double minX = Math.Min(tl.X, br.X) - 400, maxX = Math.Max(tl.X, br.X) + 400;
        double minY = Math.Min(tl.Y, br.Y) - 400, maxY = Math.Max(tl.Y, br.Y) + 400;
        var color = (Brush)new BrushConverter().ConvertFromString("#EF4444")!;   // reddish
        double t = 1.0 / Math.Max(Zoom, 0.4);

        foreach (var g in _model.Guides)
            _guideLayer.Children.Add(MakeGuideLine(g.Horizontal, g.Position, minX, maxX, minY, maxY, color, t, 0.9));
        if (_guidePreview is { } p)
            _guideLayer.Children.Add(MakeGuideLine(p.Horizontal, p.Pos, minX, maxX, minY, maxY, color, t, 0.6));
    }

    private static Line MakeGuideLine(bool horizontal, double pos, double minX, double maxX, double minY, double maxY,
                                      Brush color, double thickness, double opacity)
    {
        var line = new Line
        {
            Stroke = color, StrokeThickness = thickness, Opacity = opacity, IsHitTestVisible = false,
            StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 })
        };
        if (horizontal) { line.X1 = minX; line.X2 = maxX; line.Y1 = line.Y2 = pos; }
        else { line.Y1 = minY; line.Y2 = maxY; line.X1 = line.X2 = pos; }
        return line;
    }

    // ---------- Snapshot / undo ----------

    private void Snapshot()
    {
        _undoStack.Push(Persistence.ToJson(_model));
        if (_undoStack.Count > 100)
        {
            var arr = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var s in arr.Take(100).Reverse()) _undoStack.Push(s);
        }
        _redoStack.Clear();
        ModelDirty?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(Persistence.ToJson(_model));
        var prev = _undoStack.Pop();
        _model = JsonSerializer.Deserialize<DiagramModel>(prev, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        _selected.Clear();
        _selectedConnectionId = null;
        Rebuild();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ModelDirty?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(Persistence.ToJson(_model));
        var next = _redoStack.Pop();
        _model = JsonSerializer.Deserialize<DiagramModel>(next, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        _selected.Clear();
        _selectedConnectionId = null;
        Rebuild();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ModelDirty?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Shape add / visual ----------

    public ShapeNode AddShape(ShapeKind kind, double x, double y, double? w = null, double? h = null, string? label = null)
    {
        Snapshot();
        var (dw, dh) = DefaultSize(kind);
        var node = new ShapeNode
        {
            Kind = kind,
            X = x - (w ?? dw) / 2.0,
            Y = y - (h ?? dh) / 2.0,
            Width = w ?? dw,
            Height = h ?? dh,
            Label = label ?? DefaultLabel(kind),
            ZIndex = _nextZ++
        };
        _model.Shapes.Add(node);
        AddShapeVisual(node);
        SelectOnly(node.Id);
        return node;
    }

    /// Drops a cloud-service tile (provider badge + name) at the current viewport center.
    public ShapeNode AddServiceTile(string stencilId, string name, string color)
    {
        Snapshot();
        const double w = 120, h = 96;
        var center = (ActualWidth > 0 && ActualHeight > 0)
            ? ScreenToWorld(new Point(ActualWidth / 2, ActualHeight / 2))
            : new Point(0, 0);
        var node = new ShapeNode
        {
            Kind = ShapeKind.ServiceTile,
            Stencil = stencilId,
            X = center.X - w / 2, Y = center.Y - h / 2,
            Width = w, Height = h,
            Label = name,
            Fill = "#FFFFFF",
            Stroke = color,
            ZIndex = _nextZ++
        };
        _model.Shapes.Add(node);
        AddShapeVisual(node);
        SelectOnly(node.Id);
        return node;
    }

    /// Drops an image (data URL) at the current viewport center, at the given size.
    public ShapeNode AddImage(string dataUrl, double w, double h)
    {
        var center = (ActualWidth > 0 && ActualHeight > 0)
            ? ScreenToWorld(new Point(ActualWidth / 2, ActualHeight / 2))
            : new Point(0, 0);
        return AddImageAt(dataUrl, w, h, center);
    }

    /// Drops an image (data URL) centered on the given world point.
    private ShapeNode AddImageAt(string dataUrl, double w, double h, Point center)
    {
        Snapshot();
        var node = new ShapeNode
        {
            Kind = ShapeKind.Image,
            Image = dataUrl,
            X = center.X - w / 2, Y = center.Y - h / 2,
            Width = w, Height = h,
            Label = "",
            ZIndex = _nextZ++
        };
        _model.Shapes.Add(node);
        AddShapeVisual(node);
        SelectOnly(node.Id);
        return node;
    }

    private static (double, double) DefaultSize(ShapeKind k) => k switch
    {
        ShapeKind.Ellipse       => (140, 70),
        ShapeKind.Diamond       => (130, 100),
        ShapeKind.Hexagon       => (160, 80),
        ShapeKind.Cylinder      => (120, 90),
        ShapeKind.Cloud         => (150, 90),
        ShapeKind.Server        => (110, 130),
        ShapeKind.Person        => (90, 110),
        ShapeKind.Queue         => (160, 50),
        ShapeKind.Note          => (130, 100),
        ShapeKind.Text          => (120, 30),
        ShapeKind.ServiceTile   => (120, 96),
        ShapeKind.Image         => (160, 120),
        ShapeKind.RichText      => (200, 110),
        _                       => (140, 70)
    };

    private static string DefaultLabel(ShapeKind k) => L10n.T(k switch
    {
        ShapeKind.Rectangle     => "default.process",
        ShapeKind.Rounded       => "default.component",
        ShapeKind.Ellipse       => "default.start",
        ShapeKind.Diamond       => "default.decision",
        ShapeKind.Hexagon       => "default.step",
        ShapeKind.Parallelogram => "default.data",
        ShapeKind.Cylinder      => "default.database",
        ShapeKind.Cloud         => "default.cloud",
        ShapeKind.Server        => "default.server",
        ShapeKind.Person        => "default.user",
        ShapeKind.Queue         => "default.queue",
        ShapeKind.Note          => "default.note",
        ShapeKind.Text          => "default.text",
        ShapeKind.RichText      => "default.richtext",
        _ => "default.process"
    });

    private void AddShapeVisual(ShapeNode node)
    {
        var v = new ShapeVisual(node);
        v.Rebuild();
        v.Element.MouseEnter += (s, e) => OnShapeMouseEnter(node);
        v.Element.MouseLeave += (s, e) => OnShapeMouseLeave(node);
        // NOTE: do NOT attach MouseDown/Up on the shape — the outer canvas captures
        // the mouse on press, so handlers here never fire. Double-click is handled
        // centrally in OnMouseLeftDown via e.ClickCount.

        Canvas.SetLeft(v.Element, node.X);
        Canvas.SetTop(v.Element, node.Y);
        _shapeLayer.Children.Add(v.Element);
        _shapeVisuals[node.Id] = v;
    }

    private void OnShapeMouseEnter(ShapeNode node)
    {
        if (_drag == DragMode.ConnectDrag && _connectFromId != null && _connectFromId != node.Id)
        {
            if (_shapeVisuals.TryGetValue(node.Id, out var v))
                v.SetConnectTarget(true);
        }
    }

    private void OnShapeMouseLeave(ShapeNode node)
    {
        if (_shapeVisuals.TryGetValue(node.Id, out var v))
            v.SetConnectTarget(false);
    }

    // ---------- Connection visual ----------

    public Connection AddConnection(string fromId, string toId)
    {
        if (fromId == toId) throw new InvalidOperationException("Cannot connect a shape to itself");
        Snapshot();
        var conn = new Connection
        {
            FromId = fromId, ToId = toId,
            Routing = AppSettings.Current.DefaultRouting,
            StrokeStyle = AppSettings.Current.DefaultStroke,
            Dashed = AppSettings.Current.DefaultStroke == StrokeStyle.Dashed
        };
        _model.Connections.Add(conn);
        AddConnectionVisual(conn);
        return conn;
    }

    private void AddConnectionVisual(Connection conn)
    {
        var v = new ConnectionVisual(conn);
        v.HitPath.MouseLeftButtonDown += (s, e) =>
        {
            SelectConnection(conn.Id);
            e.Handled = true;
        };
        _connLayer.Children.Add(v.HitPath);
        _connLayer.Children.Add(v.Path);
        _connLayer.Children.Add(v.Arrow);
        _connVisuals[conn.Id] = v;
        RouteConnection(conn);
    }

    private void RouteConnection(Connection conn)
    {
        if (!_connVisuals.TryGetValue(conn.Id, out var v)) return;
        var from = _model.FindShape(conn.FromId);
        var to = _model.FindShape(conn.ToId);
        if (from == null || to == null) return;

        var fromPt = ShapeFactory.EdgeIntersect(from, new Point(to.CenterX, to.CenterY));
        var toPt = ShapeFactory.EdgeIntersect(to, new Point(from.CenterX, from.CenterY));

        v.SetEndpoints(fromPt, toPt);
    }

    private void RouteConnectionsFor(string shapeId)
    {
        foreach (var c in _model.Connections)
            if (c.FromId == shapeId || c.ToId == shapeId)
                RouteConnection(c);
    }

    // ---------- Selection ----------

    public void SelectOnly(string id)
    {
        ClearSelection(suppressEvent: true);
        _selected.Add(id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectConnection(string id)
    {
        ClearSelection(suppressEvent: true);
        _selectedConnectionId = id;
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleSelect(string id)
    {
        if (_selected.Contains(id)) _selected.Remove(id);
        else _selected.Add(id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection(bool suppressEvent = false)
    {
        _selected.Clear();
        _selectedConnectionId = null;
        ApplySelectionStyles();
        RebuildOverlay();
        if (!suppressEvent) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplySelectionStyles()
    {
        foreach (var (id, v) in _shapeVisuals)
            v.SetSelected(_selected.Contains(id));
        foreach (var (id, v) in _connVisuals)
            v.SetSelected(_selectedConnectionId == id);
    }

    // ---------- Overlay (handles, marquee) ----------

    private void RebuildOverlay()
    {
        _overlayLayer.Children.Clear();

        // A selected curved connection gets a draggable control handle (shapes and
        // connections are never selected at the same time).
        if (_selectedConnectionId != null)
        {
            BuildCurveHandle();
            return;
        }

        // Draw selection box around bounding rect of selected shapes
        if (_selected.Count == 0) return;

        var rects = _selected.Select(id => _model.FindShape(id))
                             .Where(s => s != null)
                             .Select(s => new Rect(s!.X, s.Y, s.Width, s.Height))
                             .ToList();
        if (rects.Count == 0) return;

        var bounds = rects.Aggregate((a, b) => Rect.Union(a, b));
        var pad = 4;
        var sel = new Rectangle
        {
            Width = bounds.Width + pad * 2,
            Height = bounds.Height + pad * 2,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }),
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(sel, bounds.X - pad);
        Canvas.SetTop(sel, bounds.Y - pad);
        _overlayLayer.Children.Add(sel);

        // Resize handles only when a single shape is selected
        if (_selected.Count == 1)
        {
            var id = _selected.First();
            var shape = _model.FindShape(id);
            if (shape == null) return;
            var bx = shape.X; var by = shape.Y;
            var bw = shape.Width; var bh = shape.Height;
            var handleSize = 8.0 / Math.Max(Zoom, 0.4);

            (string Name, double X, double Y)[] handles =
            {
                ("nw", bx, by),
                ("n",  bx + bw/2, by),
                ("ne", bx + bw, by),
                ("e",  bx + bw, by + bh/2),
                ("se", bx + bw, by + bh),
                ("s",  bx + bw/2, by + bh),
                ("sw", bx, by + bh),
                ("w",  bx, by + bh/2)
            };
            foreach (var h in handles)
            {
                var r = new Rectangle
                {
                    Width = handleSize, Height = handleSize,
                    Fill = Brushes.White,
                    Stroke = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
                    StrokeThickness = 1.5,
                    Cursor = HandleCursor(h.Name)
                };
                Canvas.SetLeft(r, h.X - handleSize / 2);
                Canvas.SetTop(r, h.Y - handleSize / 2);
                r.IsHitTestVisible = true;
                r.Tag = h.Name;
                _overlayLayer.IsHitTestVisible = true;
                r.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    _resizeShapeId = id;
                    _resizeHandle = (string)((Rectangle)s!).Tag;
                    _resizeOrigin = (shape.X, shape.Y, shape.Width, shape.Height);
                    _drag = DragMode.ResizeShape;
                    _dragStartWorld = ScreenToWorld(e.GetPosition(this));
                    CaptureMouse();
                    Snapshot();
                };
                _overlayLayer.Children.Add(r);
            }
        }
    }

    private void BuildCurveHandle()
    {
        var c = GetSelectedConnection();
        if (c == null || c.Routing != ConnectorRouting.Curved) return;
        if (!_connVisuals.TryGetValue(c.Id, out var v)) return;

        var cp = v.ControlPoint;
        var size = 11.0 / Math.Max(Zoom, 0.4);
        var dot = new Ellipse
        {
            Width = size, Height = size,
            Fill = Brushes.White,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
            StrokeThickness = 1.6,
            Cursor = Cursors.SizeAll
        };
        Canvas.SetLeft(dot, cp.X - size / 2);
        Canvas.SetTop(dot, cp.Y - size / 2);
        _overlayLayer.IsHitTestVisible = true;
        dot.MouseLeftButtonDown += (s, e) =>
        {
            e.Handled = true;
            _drag = DragMode.ConnCurve;
            _curveConnId = c.Id;
            CaptureMouse();
            Snapshot();
        };
        _overlayLayer.Children.Add(dot);
    }

    private static Cursor HandleCursor(string name) => name switch
    {
        "n" or "s" => Cursors.SizeNS,
        "e" or "w" => Cursors.SizeWE,
        "ne" or "sw" => Cursors.SizeNESW,
        _ => Cursors.SizeNWSE
    };

    // ---------- Mouse events ----------

    private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly) return;
        if (_isEditingText) { CommitTextEdit(); }
        Focus();
        var screen = e.GetPosition(this);
        var world = ScreenToWorld(screen);

        // Handle resize first (overlay handles already capture before we get here)

        // Space + drag = pan
        if (_spaceHeld || _tool == ToolMode.Pan)
        {
            _drag = DragMode.Pan;
            _dragStartScreen = screen;
            _dragStartTransform = _worldTransform.Matrix;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // Shape tools: add at click
        var addKind = ToolModeMap.ShapeForTool(_tool);
        if (addKind.HasValue)
        {
            // Clicking an existing object switches to Select and selects it;
            // clicking empty space stamps another shape and stays in this tool.
            var hitForAdd = HitTestShape(world);
            if (hitForAdd != null)
            {
                CurrentTool = ToolMode.Select;
                SelectOnly(hitForAdd.Id);
            }
            else
            {
                AddShape(addKind.Value, world.X, world.Y);
            }
            e.Handled = true;
            return;
        }

        // Connect tool: must start on a shape
        if (_tool == ToolMode.Connect)
        {
            var hit = HitTestShape(world);
            if (hit != null)
            {
                _connectFromId = hit.Id;
                _drag = DragMode.ConnectDrag;
                _dragStartScreen = screen; // for click-vs-drag detection on mouse-up
                if (_shapeVisuals.TryGetValue(hit.Id, out var sv)) sv.SetConnectSource(true);
                _dragLine = new Path
                {
                    Stroke = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection(new[] { 5.0, 4.0 }),
                    IsHitTestVisible = false
                };
                _overlayLayer.Children.Add(_dragLine);
                CaptureMouse();
                e.Handled = true;
                return;
            }
            // Empty space in connector mode → fall through to pan (matches select-tool behavior)
        }

        // Select tool: shape hit?
        if (_tool == ToolMode.Select || _tool == ToolMode.Connect)
        {
            var hit = HitTestShape(world);
            if (hit != null && _tool == ToolMode.Select)
            {
                // Double-click → edit shape label (rich editor for rich-text shapes)
                if (e.ClickCount == 2)
                {
                    SelectOnly(hit.Id);
                    if (hit.Kind == ShapeKind.RichText) BeginEditRichText(hit);
                    else BeginEditText(hit);
                    e.Handled = true;
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    ToggleSelect(hit.Id);
                else if (!_selected.Contains(hit.Id))
                    SelectOnly(hit.Id);

                // Begin move
                _drag = DragMode.MoveShape;
                _dragStartWorld = world;
                _dragOrigins.Clear();
                foreach (var id in _selected)
                {
                    var s = _model.FindShape(id);
                    if (s != null) _dragOrigins[id] = (s.X, s.Y);
                }
                Snapshot();
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // Empty space, Select tool: grab a nearby guide to move/remove it.
            if (_tool == ToolMode.Select && HitTestGuide(world) is { } guide)
            {
                _dragGuide = guide;
                _drag = DragMode.GuideMove;
                Snapshot();
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // Empty space:
            //   Shift + drag → marquee selection (kept available for multi-select)
            //   default      → pan canvas
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                ClearSelection();
                _drag = DragMode.Marquee;
                _dragStartWorld = world;
                _marqueeRect = new Rectangle
                {
                    Stroke = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }),
                    Fill = (Brush)new BrushConverter().ConvertFromString("#190EA5E9")!,
                    Width = 0, Height = 0
                };
                Canvas.SetLeft(_marqueeRect, world.X);
                Canvas.SetTop(_marqueeRect, world.Y);
                _overlayLayer.Children.Add(_marqueeRect);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // Default empty-area behavior: pan
            ClearSelection();
            _drag = DragMode.Pan;
            _dragStartScreen = screen;
            _dragStartTransform = _worldTransform.Matrix;
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ReadOnly) return;
        var screen = e.GetPosition(this);
        var world = ScreenToWorld(screen);
        _lastMouseWorld = world;
        _hasMouseWorld = true;

        if (_drag == DragMode.Pan)
        {
            var dx = screen.X - _dragStartScreen.X;
            var dy = screen.Y - _dragStartScreen.Y;
            var m = _dragStartTransform;
            m.OffsetX += dx;
            m.OffsetY += dy;
            _worldTransform.Matrix = m;
            UpdateGridOffset();
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_drag == DragMode.ConnCurve && _curveConnId != null)
        {
            var c = _model.Connections.FirstOrDefault(x => x.Id == _curveConnId);
            if (c != null && _connVisuals.TryGetValue(c.Id, out var cv))
            {
                c.CurveDX = world.X - cv.MidPoint.X;
                c.CurveDY = world.Y - cv.MidPoint.Y;
                RouteConnection(c);
                RebuildOverlay();
            }
            return;
        }

        if (_drag == DragMode.GuideMove && _dragGuide != null)
        {
            _dragGuide.Position = _dragGuide.Horizontal ? world.Y : world.X;
            RebuildGuides();
            return;
        }

        if (_drag == DragMode.MoveShape)
        {
            var dx = world.X - _dragStartWorld.X;
            var dy = world.Y - _dragStartWorld.Y;

            // Snap the selection's edges/centers to other shapes and guides (unless Alt held).
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                (dx, dy) = ComputeMoveSnap(dx, dy);
            else { _snapX = null; _snapY = null; }

            foreach (var (id, origin) in _dragOrigins)
            {
                var s = _model.FindShape(id);
                if (s == null) continue;
                s.X = origin.X + dx;
                s.Y = origin.Y + dy;
                if (_shapeVisuals.TryGetValue(id, out var v))
                {
                    Canvas.SetLeft(v.Element, s.X);
                    Canvas.SetTop(v.Element, s.Y);
                }
                RouteConnectionsFor(id);
            }
            RebuildOverlay();
            DrawSnapGuides();
            return;
        }

        if (_drag == DragMode.ResizeShape && _resizeShapeId != null)
        {
            var shape = _model.FindShape(_resizeShapeId);
            if (shape == null) return;
            var dx = world.X - _dragStartWorld.X;
            var dy = world.Y - _dragStartWorld.Y;
            var o = _resizeOrigin;
            double nx = o.X, ny = o.Y, nw = o.W, nh = o.H;
            if (_resizeHandle.Contains('e')) nw = Math.Max(20, o.W + dx);
            if (_resizeHandle.Contains('s')) nh = Math.Max(20, o.H + dy);
            if (_resizeHandle.Contains('w')) { nx = o.X + dx; nw = Math.Max(20, o.W - dx); if (nw == 20) nx = o.X + o.W - 20; }
            if (_resizeHandle.Contains('n')) { ny = o.Y + dy; nh = Math.Max(20, o.H - dy); if (nh == 20) ny = o.Y + o.H - 20; }
            shape.X = nx; shape.Y = ny; shape.Width = nw; shape.Height = nh;
            if (_shapeVisuals.TryGetValue(shape.Id, out var v))
            {
                Canvas.SetLeft(v.Element, nx);
                Canvas.SetTop(v.Element, ny);
                v.Rebuild();
            }
            RouteConnectionsFor(shape.Id);
            RebuildOverlay();
            return;
        }

        if (_drag == DragMode.ConnectDrag && _connectFromId != null && _dragLine != null)
        {
            var from = _model.FindShape(_connectFromId);
            if (from == null) return;
            var fromPt = ShapeFactory.EdgeIntersect(from, world);
            _dragLine.Data = Geometry.Parse(FormattableString.Invariant($"M {fromPt.X},{fromPt.Y} L {world.X},{world.Y}"));
            return;
        }

        if (_drag == DragMode.Marquee && _marqueeRect != null)
        {
            var x = Math.Min(_dragStartWorld.X, world.X);
            var y = Math.Min(_dragStartWorld.Y, world.Y);
            var w = Math.Abs(world.X - _dragStartWorld.X);
            var h = Math.Abs(world.Y - _dragStartWorld.Y);
            Canvas.SetLeft(_marqueeRect, x);
            Canvas.SetTop(_marqueeRect, y);
            _marqueeRect.Width = w;
            _marqueeRect.Height = h;
            // Live select
            _selected.Clear();
            var marqueeRect = new Rect(x, y, w, h);
            foreach (var s in _model.Shapes)
            {
                var sr = new Rect(s.X, s.Y, s.Width, s.Height);
                if (marqueeRect.IntersectsWith(sr)) _selected.Add(s.Id);
            }
            ApplySelectionStyles();
            return;
        }

        // Hover feedback: show a move cursor over a draggable guide (Select tool, no drag).
        if (_drag == DragMode.None && _tool == ToolMode.Select && _model.Guides.Count > 0
            && HitTestShape(world) == null && HitTestGuide(world) is { } hg)
            Cursor = hg.Horizontal ? Cursors.SizeNS : Cursors.SizeWE;
        else if (_drag == DragMode.None)
            UpdateCursor();
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly) return;
        var world = ScreenToWorld(e.GetPosition(this));

        if (_drag == DragMode.GuideMove && _dragGuide != null)
        {
            // Dropped back over the ruler (off the canvas edge) → remove the guide.
            var sp = e.GetPosition(this);
            bool removed = _dragGuide.Horizontal ? sp.Y < 0 : sp.X < 0;
            if (removed) _model.Guides.Remove(_dragGuide);
            RebuildGuides();
        }

        if (_drag == DragMode.ConnectDrag && _connectFromId != null)
        {
            var target = HitTestShape(world);
            if (_shapeVisuals.TryGetValue(_connectFromId, out var sv)) sv.SetConnectSource(false);
            foreach (var v in _shapeVisuals.Values) v.SetConnectTarget(false);

            // Was this a click (almost no drag) instead of a real drag?
            var screen = e.GetPosition(this);
            var travel = Math.Sqrt(
                Math.Pow(screen.X - _dragStartScreen.X, 2) +
                Math.Pow(screen.Y - _dragStartScreen.Y, 2));
            var sourceId = _connectFromId;

            if (_dragLine != null) _overlayLayer.Children.Remove(_dragLine);
            _dragLine = null;
            _connectFromId = null;

            if (travel < 4)
            {
                // Treat as a click: switch to Select tool and select the shape.
                CurrentTool = ToolMode.Select;
                SelectOnly(sourceId);
            }
            else if (target != null && target.Id != sourceId)
            {
                AddConnection(sourceId, target.Id);
            }
            // Otherwise: drag fizzled (released on empty space). Stay in connector mode.
        }

        if (_drag == DragMode.Marquee && _marqueeRect != null)
        {
            _overlayLayer.Children.Remove(_marqueeRect);
            _marqueeRect = null;
            RebuildOverlay();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        _drag = DragMode.None;
        _resizeShapeId = null;
        _curveConnId = null;
        _dragGuide = null;
        _dragOrigins.Clear();
        if (IsMouseCaptured) ReleaseMouseCapture();
        UpdateCursor();
        if (_snapX != null || _snapY != null) { _snapX = null; _snapY = null; RebuildOverlay(); }
    }

    private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly) return;
        if (_isEditingText) CommitTextEdit();
        Focus();
        var screen = e.GetPosition(this);
        var world = ScreenToWorld(screen);

        // Right-click on a shape → select it and arm a context menu (raised on release
        // if the user didn't drag). Right-click on empty space → quick pan, as before.
        var hit = HitTestShape(world);
        if (hit != null)
        {
            if (!_selected.Contains(hit.Id)) SelectOnly(hit.Id);
            _rightClickPending = true;
            _dragStartScreen = screen;
            e.Handled = true;
            return;
        }

        // Right-click on a connector → select it and arm its context menu.
        var connId = HitTestConnection(world);
        if (connId != null)
        {
            SelectConnection(connId);
            _rightClickConn = true;
            _dragStartScreen = screen;
            e.Handled = true;
            return;
        }

        _drag = DragMode.Pan;
        _dragStartScreen = screen;
        _dragStartTransform = _worldTransform.Matrix;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightUp(object sender, MouseButtonEventArgs e)
    {
        if (ReadOnly) return;
        if (_rightClickPending)
        {
            _rightClickPending = false;
            var screen = e.GetPosition(this);
            // Only treat as a context-menu click if the pointer barely moved.
            if ((screen - _dragStartScreen).Length < 6)
                ContextMenuRequested?.Invoke(this, screen);
        }

        if (_rightClickConn)
        {
            _rightClickConn = false;
            var screen = e.GetPosition(this);
            if ((screen - _dragStartScreen).Length < 6)
                ConnectionContextRequested?.Invoke(this, screen);
        }

        if (_drag == DragMode.Pan)
        {
            _drag = DragMode.None;
            if (IsMouseCaptured) ReleaseMouseCapture();
            UpdateCursor();
        }
        e.Handled = true;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ReadOnly) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl + wheel = zoom toward the cursor.
            var screen = e.GetPosition(this);
            var oldZoom = _worldTransform.Matrix.M11;
            var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
            var newZoom = Math.Max(0.2, Math.Min(4.0, oldZoom * factor));
            var scale = newZoom / oldZoom;
            var mz = _worldTransform.Matrix;
            mz.ScaleAt(scale, scale, screen.X, screen.Y);
            _worldTransform.Matrix = mz;
            UpdateGridOffset();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            ViewChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        // Plain wheel scrolls vertically; Shift + wheel scrolls horizontally.
        var m = _worldTransform.Matrix;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            m.OffsetX += e.Delta;
        else
            m.OffsetY += e.Delta;
        _worldTransform.Matrix = m;
        UpdateGridOffset();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    // ---------- Keyboard ----------

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (_isEditingText) return;
        if (e.Key == Key.Space) { _spaceHeld = true; UpdateCursor(); return; }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.Z) { Undo(); e.Handled = true; return; }
            if (e.Key == Key.Y) { Redo(); e.Handled = true; return; }
            if (e.Key == Key.D) { DuplicateSelection(); e.Handled = true; return; }
            if (e.Key == Key.A) { SelectAll(); e.Handled = true; return; }
            if (e.Key == Key.C) { Copy();  e.Handled = true; return; }
            if (e.Key == Key.X) { Cut();   e.Handled = true; return; }
            if (e.Key == Key.V) { Paste(); e.Handled = true; return; }
            return;
        }

        switch (e.Key)
        {
            case Key.Delete: case Key.Back: DeleteSelection(); break;
            case Key.Escape: ClearSelection(); CurrentTool = ToolMode.Select; break;
            case Key.V: CurrentTool = ToolMode.Select; break;
            case Key.L: CurrentTool = ToolMode.Connect; break;
            case Key.R: CurrentTool = ToolMode.AddRectangle; break;
            case Key.O: CurrentTool = ToolMode.AddRounded; break;
            case Key.E: CurrentTool = ToolMode.AddEllipse; break;
            case Key.D: CurrentTool = ToolMode.AddDiamond; break;
            case Key.H: CurrentTool = ToolMode.AddHexagon; break;
            case Key.B: CurrentTool = ToolMode.AddCylinder; break;
            case Key.C: CurrentTool = ToolMode.AddCloud; break;
            case Key.S: CurrentTool = ToolMode.AddServer; break;
            case Key.P: CurrentTool = ToolMode.AddPerson; break;
            case Key.T: CurrentTool = ToolMode.AddText; break;
        }
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space) { _spaceHeld = false; UpdateCursor(); }
    }

    // ---------- Edit ops ----------

    public void DeleteSelection()
    {
        if (_selected.Count == 0 && _selectedConnectionId == null) return;
        Snapshot();
        if (_selectedConnectionId != null)
        {
            _model.Connections.RemoveAll(c => c.Id == _selectedConnectionId);
            if (_connVisuals.TryGetValue(_selectedConnectionId, out var v))
            {
                _connLayer.Children.Remove(v.Path);
                _connLayer.Children.Remove(v.Arrow);
                _connLayer.Children.Remove(v.HitPath);
                _connVisuals.Remove(_selectedConnectionId);
            }
            _selectedConnectionId = null;
        }
        foreach (var id in _selected.ToList())
        {
            _model.Shapes.RemoveAll(s => s.Id == id);
            _model.Connections.RemoveAll(c => c.FromId == id || c.ToId == id);
            if (_shapeVisuals.TryGetValue(id, out var v))
            {
                _shapeLayer.Children.Remove(v.Element);
                _shapeVisuals.Remove(id);
            }
        }
        // Remove orphan connection visuals
        foreach (var cid in _connVisuals.Keys.ToList())
        {
            if (!_model.Connections.Any(c => c.Id == cid))
            {
                var cv = _connVisuals[cid];
                _connLayer.Children.Remove(cv.Path);
                _connLayer.Children.Remove(cv.Arrow);
                _connLayer.Children.Remove(cv.HitPath);
                _connVisuals.Remove(cid);
            }
        }
        _selected.Clear();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DuplicateSelection()
    {
        if (_selected.Count == 0) return;
        Snapshot();
        var newIds = new List<string>();
        foreach (var id in _selected.ToList())
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            var copy = new ShapeNode
            {
                Kind = s.Kind, X = s.X + 24, Y = s.Y + 24,
                Width = s.Width, Height = s.Height,
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke,
                Stencil = s.Stencil,
                Image = s.Image,
                ZIndex = _nextZ++
            };
            _model.Shapes.Add(copy);
            AddShapeVisual(copy);
            newIds.Add(copy.Id);
        }
        _selected.Clear();
        foreach (var id in newIds) _selected.Add(id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Clipboard ----------

    public void Copy()
    {
        if (_selected.Count == 0) return;

        var snippet = new DiagramModel { Title = "clipboard" };
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            // Keep original ids so connections can reference them inside the snippet
            snippet.Shapes.Add(new ShapeNode
            {
                Id = s.Id, Kind = s.Kind,
                X = s.X, Y = s.Y, Width = s.Width, Height = s.Height,
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke,
                Stencil = s.Stencil,
                Image = s.Image,
                ZIndex = s.ZIndex
            });
        }
        // Include connections whose endpoints are both in the selection
        foreach (var c in _model.Connections)
        {
            if (_selected.Contains(c.FromId) && _selected.Contains(c.ToId))
                snippet.Connections.Add(new Connection
                {
                    FromId = c.FromId, ToId = c.ToId,
                    Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed,
                    Routing = c.Routing, StrokeStyle = c.StrokeStyle, CurveDX = c.CurveDX, CurveDY = c.CurveDY
                });
        }

        try
        {
            var json = Persistence.ToJson(snippet);
            var data = new System.Windows.DataObject();
            data.SetData(ClipboardFormat, json);
            data.SetData(System.Windows.DataFormats.UnicodeText, json);
            System.Windows.Clipboard.SetDataObject(data, copy: true);
        }
        catch (Exception) { /* the clipboard is sometimes briefly unavailable — never crash */ }
    }

    public void Cut()
    {
        if (_selected.Count == 0) return;
        Copy();
        DeleteSelection();
    }

    public void Paste()
    {
        string? json = null;
        try
        {
            if (System.Windows.Clipboard.ContainsData(ClipboardFormat))
                json = System.Windows.Clipboard.GetData(ClipboardFormat) as string;
        }
        catch (Exception) { /* fall through to image/text below */ }

        // No DrawThisEasy snippet on the clipboard — fall back to pasting an image
        // (from PowerPoint, screenshots, browsers, etc.) or plain text as a Text shape.
        if (string.IsNullOrWhiteSpace(json))
        {
            // Decide between an editable Text shape and an Image. A real text selection
            // from Office carries Rich Text Format; plain text with no image at all is a
            // Notepad-style copy. Either way → Text shape. A picture/shape/slide/screenshot
            // (image, but no RTF) → Image. PowerPoint also tags text copies with a metafile
            // and bitmap, so "has an image" alone can't be trusted — RTF is the tell.
            if (ClipboardLooksLikeText())
            {
                if (TryPasteClipboardRtf()) return;   // formatted text (e.g. PowerPoint) → rich-text shape
                if (TryPasteClipboardText()) return;
                if (TryPasteClipboardImage()) return;
            }
            else
            {
                if (TryPasteClipboardImage()) return;
                if (TryPasteClipboardRtf()) return;
                if (TryPasteClipboardText()) return;
            }
            return;
        }

        DiagramModel? snippet;
        try
        {
            snippet = JsonSerializer.Deserialize<DiagramModel>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch { return; }

        if (snippet == null || snippet.Shapes.Count == 0) return;

        Snapshot();

        // Compute snippet's bounding-box centroid
        var minX = snippet.Shapes.Min(s => s.X);
        var minY = snippet.Shapes.Min(s => s.Y);
        var maxX = snippet.Shapes.Max(s => s.X + s.Width);
        var maxY = snippet.Shapes.Max(s => s.Y + s.Height);
        var cx = (minX + maxX) / 2.0;
        var cy = (minY + maxY) / 2.0;

        // Target paste center: cursor if it's over the canvas, otherwise viewport center.
        Point target;
        if (IsMouseOver && _hasMouseWorld)
            target = _lastMouseWorld;
        else if (ActualWidth > 0 && ActualHeight > 0)
            target = ScreenToWorld(new Point(ActualWidth / 2, ActualHeight / 2));
        else
            target = new Point(cx + 24, cy + 24);

        var dx = target.X - cx;
        var dy = target.Y - cy;

        var idMap = new Dictionary<string, string>();
        var newIds = new List<string>();
        foreach (var s in snippet.Shapes)
        {
            var copy = new ShapeNode
            {
                Kind = s.Kind,
                X = s.X + dx, Y = s.Y + dy,
                Width = s.Width, Height = s.Height,
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke,
                Stencil = s.Stencil,
                Image = s.Image,
                ZIndex = _nextZ++
            };
            idMap[s.Id] = copy.Id;
            _model.Shapes.Add(copy);
            AddShapeVisual(copy);
            newIds.Add(copy.Id);
        }
        foreach (var c in snippet.Connections)
        {
            if (!idMap.TryGetValue(c.FromId, out var fromId) ||
                !idMap.TryGetValue(c.ToId,   out var toId)) continue;
            var conn = new Connection
            {
                FromId = fromId, ToId = toId,
                Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed,
                Routing = c.Routing, StrokeStyle = c.StrokeStyle, CurveDX = c.CurveDX, CurveDY = c.CurveDY
            };
            _model.Connections.Add(conn);
            AddConnectionVisual(conn);
        }

        _selected.Clear();
        _selectedConnectionId = null;
        foreach (var id in newIds) _selected.Add(id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// Where a paste should land: the cursor if it's over the canvas, else the viewport center.
    private Point PasteCenter()
    {
        if (IsMouseOver && _hasMouseWorld) return _lastMouseWorld;
        if (ActualWidth > 0 && ActualHeight > 0)
            return ScreenToWorld(new Point(ActualWidth / 2, ActualHeight / 2));
        return new Point(0, 0);
    }

    /// True when the clipboard is best treated as a text selection rather than a picture.
    /// Office text copies carry Rich Text Format (even though PowerPoint also adds a
    /// metafile + bitmap); a Notepad-style copy is plain text with no image at all. A
    /// copied shape/picture/slide/screenshot has an image but no RTF, so it is not text.
    private static bool ClipboardLooksLikeText()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText()) return false;
            if (System.Windows.Clipboard.ContainsData(DataFormats.Rtf)) return true;
            return !System.Windows.Clipboard.ContainsImage()
                && !System.Windows.Clipboard.ContainsData("PNG");
        }
        catch { return false; }
    }

    /// Pastes a bitmap off the system clipboard (PowerPoint, screenshots, browsers, …)
    /// as an Image shape. Returns false if the clipboard holds no image.
    private bool TryPasteClipboardImage()
    {
        byte[]? bytes = null;
        double pw = 0, ph = 0;

        // Prefer the raw "PNG" payload Office/browsers put on the clipboard — it keeps the
        // full resolution and alpha, whereas Clipboard.GetImage() round-trips through a DIB
        // that frequently comes back downscaled.
        try
        {
            if (System.Windows.Clipboard.ContainsData("PNG"))
            {
                var data = System.Windows.Clipboard.GetData("PNG");
                if (data is System.IO.MemoryStream ms) bytes = ms.ToArray();
                else if (data is byte[] b) bytes = b;
            }
        }
        catch { bytes = null; }

        if (bytes is { Length: > 0 })
        {
            try
            {
                var probe = new BitmapImage();
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    probe.BeginInit();
                    probe.CacheOption = BitmapCacheOption.OnLoad;
                    probe.StreamSource = ms;
                    probe.EndInit();
                }
                pw = probe.PixelWidth; ph = probe.PixelHeight;
            }
            catch { bytes = null; }
        }

        // Fall back to the DIB bitmap when there's no usable PNG payload.
        if (bytes is not { Length: > 0 })
        {
            BitmapSource? src = null;
            try
            {
                if (System.Windows.Clipboard.ContainsImage())
                    src = System.Windows.Clipboard.GetImage();
            }
            catch { return false; }
            if (src == null) return false;

            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(src));
                using var ms = new System.IO.MemoryStream();
                encoder.Save(ms);
                bytes = ms.ToArray();
            }
            catch { return false; }
            pw = src.PixelWidth; ph = src.PixelHeight;
        }

        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

        // Display the image at its pixel size, scaled down to a sensible default. The full
        // resolution stays in the data URL, so resizing it back up stays crisp.
        double w = 200, h = 150;
        if (pw > 0 && ph > 0)
        {
            double scale = Math.Min(1.0, 320.0 / Math.Max(pw, ph));
            w = Math.Max(24, pw * scale);
            h = Math.Max(24, ph * scale);
        }
        AddImageAt(dataUrl, w, h, PasteCenter());
        return true;
    }

    /// Pastes plain text off the system clipboard as a Text shape.
    /// Returns false if the clipboard holds no (non-empty) text.
    private bool TryPasteClipboardText()
    {
        string? text = null;
        try
        {
            if (System.Windows.Clipboard.ContainsText())
                text = System.Windows.Clipboard.GetText();
        }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        var (dw, dh) = DefaultSize(ShapeKind.Text);
        var center = PasteCenter();
        Snapshot();
        var node = new ShapeNode
        {
            Kind = ShapeKind.Text,
            X = center.X - dw / 2, Y = center.Y - dh / 2,
            Width = dw, Height = dh,
            Label = text,
            ZIndex = _nextZ++
        };
        _model.Shapes.Add(node);
        AddShapeVisual(node);
        SelectOnly(node.Id);
        return true;
    }

    /// Pastes formatted clipboard text (RTF — e.g. from PowerPoint/Word) as a rich-text
    /// shape, preserving fonts, colors and styles. Returns false if there's no RTF.
    private bool TryPasteClipboardRtf()
    {
        string? rtf = null;
        try
        {
            if (System.Windows.Clipboard.ContainsData(DataFormats.Rtf))
                rtf = System.Windows.Clipboard.GetData(DataFormats.Rtf) as string;
        }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(rtf)) return false;

        string text = "";
        try { if (System.Windows.Clipboard.ContainsText()) text = System.Windows.Clipboard.GetText().Trim(); }
        catch { /* label fallback is optional */ }

        var (dw, dh) = DefaultSize(ShapeKind.RichText);
        var center = PasteCenter();
        Snapshot();
        var node = new ShapeNode
        {
            Kind = ShapeKind.RichText,
            X = center.X - dw / 2, Y = center.Y - dh / 2,
            Width = dw, Height = dh,
            Rtf = rtf,
            Label = text,
            ZIndex = _nextZ++,
        };
        _model.Shapes.Add(node);
        AddShapeVisual(node);
        SelectOnly(node.Id);
        return true;
    }

    public void SelectAll()
    {
        _selected.Clear();
        foreach (var s in _model.Shapes) _selected.Add(s.Id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BringToFront()
    {
        if (_selected.Count == 0) return;
        Snapshot();
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            s.ZIndex = _nextZ++;
            if (_shapeVisuals.TryGetValue(id, out var v))
            {
                _shapeLayer.Children.Remove(v.Element);
                _shapeLayer.Children.Add(v.Element);
            }
        }
    }

    public void SendToBack()
    {
        if (_selected.Count == 0) return;
        Snapshot();
        int minZ = _model.Shapes.Min(s => s.ZIndex);
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            s.ZIndex = --minZ;
        }
        // Rebuild Z order in the shape layer
        var ordered = _model.Shapes.OrderBy(s => s.ZIndex).ToList();
        _shapeLayer.Children.Clear();
        foreach (var s in ordered)
            if (_shapeVisuals.TryGetValue(s.Id, out var v))
                _shapeLayer.Children.Add(v.Element);
    }

    public void SetSelectedFill(string hex)
    {
        if (_selected.Count == 0) return;
        Snapshot();
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            s.Fill = hex;
            if (_shapeVisuals.TryGetValue(id, out var v)) v.Rebuild();
        }
    }

    public void SetSelectedStroke(string hex)
    {
        if (_selected.Count == 0) return;
        Snapshot();
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            s.Stroke = hex;
            if (_shapeVisuals.TryGetValue(id, out var v)) v.Rebuild();
        }
        // Also update any connections originating/ending here? No — connection strokes are separate.
    }

    // ---- Label typography (applies to every selected shape) ----
    private void MutateSelectedShapes(Action<ShapeNode> mutate)
    {
        if (_selected.Count == 0) return;
        Snapshot();
        foreach (var id in _selected)
        {
            var s = _model.FindShape(id);
            if (s == null) continue;
            mutate(s);
            if (_shapeVisuals.TryGetValue(id, out var v)) v.Rebuild();
        }
        ApplySelectionStyles();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // When a rich-text editor is open, font controls format its (possibly inactive) selection;
    // otherwise they set the per-shape typography fields on the selected shapes.
    private bool ApplyRichProperty(DependencyProperty prop, object? value)
    {
        if (_richEditBox == null) return false;
        _richEditBox.Selection?.ApplyPropertyValue(prop, value);
        return true;
    }

    public void SetSelectedFontFamily(string family)
    {
        if (ApplyRichProperty(TextElement.FontFamilyProperty, new FontFamily(family))) return;
        MutateSelectedShapes(s => s.FontFamily = family);
    }

    public void SetSelectedFontSize(double size)
    {
        if (ApplyRichProperty(TextElement.FontSizeProperty, size)) return;
        MutateSelectedShapes(s => s.FontSize = size);
    }

    public void SetSelectedBold(bool on)
    {
        if (ApplyRichProperty(TextElement.FontWeightProperty, on ? FontWeights.Bold : FontWeights.Normal)) return;
        MutateSelectedShapes(s => s.Bold = on);
    }

    public void SetSelectedItalic(bool on)
    {
        if (ApplyRichProperty(TextElement.FontStyleProperty, on ? FontStyles.Italic : FontStyles.Normal)) return;
        MutateSelectedShapes(s => s.Italic = on);
    }

    public void SetSelectedUnderline(bool on)
    {
        if (ApplyRichProperty(Inline.TextDecorationsProperty, on ? TextDecorations.Underline : null)) return;
        MutateSelectedShapes(s => s.Underline = on);
    }

    public void SetSelectedFontColor(string hex)
    {
        Brush? brush = null;
        try { brush = (Brush)new BrushConverter().ConvertFromString(hex)!; } catch { }
        if (_richEditBox != null) { if (brush != null) ApplyRichProperty(TextElement.ForegroundProperty, brush); return; }
        MutateSelectedShapes(s => s.FontColor = hex);
    }

    public void SetSelectedTextAlign(TextAlign align)
    {
        if (_richEditBox != null)
        {
            var ta = align switch
            {
                TextAlign.Left  => TextAlignment.Left,
                TextAlign.Right => TextAlignment.Right,
                _               => TextAlignment.Center,
            };
            ApplyRichProperty(Block.TextAlignmentProperty, ta);
            return;
        }
        MutateSelectedShapes(s => s.TextAlign = align);
    }

    public ShapeNode? GetSelectedShape() => _selected.Count == 1 ? _model.FindShape(_selected.First()) : null;

    /// First shape in the current selection (any count), used to seed the font inspector.
    public ShapeNode? PrimarySelectedShape => _selected.Count > 0 ? _model.FindShape(_selected.First()) : null;

    public Connection? GetSelectedConnection() =>
        _selectedConnectionId == null ? null : _model.Connections.FirstOrDefault(c => c.Id == _selectedConnectionId);

    public void SetSelectedConnectionRouting(ConnectorRouting routing)
    {
        var c = GetSelectedConnection();
        if (c == null) return;
        Snapshot();
        c.Routing = routing;
        if (routing != ConnectorRouting.Curved) { c.CurveDX = 0; c.CurveDY = 0; }
        RouteConnection(c);
        RebuildOverlay();
    }

    public void SetSelectedConnectionStroke(StrokeStyle style)
    {
        var c = GetSelectedConnection();
        if (c == null) return;
        Snapshot();
        c.StrokeStyle = style;
        c.Dashed = style == StrokeStyle.Dashed;   // keep the legacy flag consistent
        RouteConnection(c);
    }

    // ---------- Text edit ----------

    private TextBox? _textEditBox;
    private RichTextBox? _richEditBox;
    private string? _editingShapeId;
    private string? _editingConnectionId;

    /// True while an overlay RichTextBox is open for editing a rich-text shape.
    public bool IsEditingRichText => _richEditBox != null;

    public void BeginEditText(ShapeNode node)
    {
        EndAnyEdit();
        _editingShapeId = node.Id;
        _isEditingText = true;
        _textEditBox = new TextBox
        {
            Text = node.Label,
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI"),
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
            BorderThickness = new Thickness(2),
            TextAlignment = TextAlignment.Center,
            MinWidth = 80
        };
        _textEditBox.Width = node.Width;
        _textEditBox.Height = Math.Min(node.Height, 28);
        _textEditBox.SelectAll();
        _textEditBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape) { CommitTextEdit(); e.Handled = true; }
        };
        _textEditBox.LostFocus += (s, e) => CommitTextEdit();

        Canvas.SetLeft(_textEditBox, node.X);
        Canvas.SetTop(_textEditBox, node.Y + (node.Height / 2.0) - 14);
        _overlayLayer.IsHitTestVisible = true;
        _overlayLayer.Children.Add(_textEditBox);
        _textEditBox.Focus();
    }

    /// Opens an editable RichTextBox over a rich-text shape. Ctrl+B/I/U and pasting
    /// formatted content (e.g. from PowerPoint) work natively; Escape or clicking away
    /// commits. The Inspector typography controls route to this editor's selection.
    public void BeginEditRichText(ShapeNode node)
    {
        EndAnyEdit();
        _editingShapeId = node.Id;
        _isEditingText = true;
        _richEditBox = new RichTextBox
        {
            Width = node.Width,
            Height = node.Height,
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(6, 4, 6, 4),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = (Brush)new BrushConverter().ConvertFromString("#0F172A")!,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            AcceptsReturn = true,
            // Keep the selection visible when focus moves to the Inspector font controls,
            // so those controls can format the still-selected text.
            IsInactiveSelectionHighlightEnabled = true,
        };
        _richEditBox.Document.PagePadding = new Thickness(0);
        ShapeFactory.LoadRich(_richEditBox, node.Rtf, node.Label);
        // Commit on Escape; clicking elsewhere on the canvas also commits (OnMouseLeftDown).
        // We deliberately do NOT commit on LostFocus, so the Inspector can format the selection.
        _richEditBox.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) { CommitTextEdit(); e.Handled = true; }
        };

        Canvas.SetLeft(_richEditBox, node.X);
        Canvas.SetTop(_richEditBox, node.Y);
        _overlayLayer.IsHitTestVisible = true;
        _overlayLayer.Children.Add(_richEditBox);
        _richEditBox.Focus();
        _richEditBox.SelectAll();
    }

    public void BeginEditConnectionText(Connection conn)
    {
        EndAnyEdit();
        if (!_connVisuals.TryGetValue(conn.Id, out var v)) return;
        _editingConnectionId = conn.Id;
        _isEditingText = true;
        var mid = v.MidPoint;
        _textEditBox = new TextBox
        {
            Text = conn.Label,
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI"),
            Background = Brushes.White,
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!,
            BorderThickness = new Thickness(2),
            TextAlignment = TextAlignment.Center,
            Width = 120, Height = 24
        };
        _textEditBox.SelectAll();
        _textEditBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape) { CommitTextEdit(); e.Handled = true; }
        };
        _textEditBox.LostFocus += (s, e) => CommitTextEdit();
        Canvas.SetLeft(_textEditBox, mid.X - 60);
        Canvas.SetTop(_textEditBox, mid.Y - 12);
        _overlayLayer.IsHitTestVisible = true;
        _overlayLayer.Children.Add(_textEditBox);
        _textEditBox.Focus();
    }

    private void CommitTextEdit()
    {
        if (!_isEditingText) return;

        // Rich-text shape: persist the document as RTF (plus a plain-text Label fallback).
        if (_richEditBox != null && _editingShapeId != null)
        {
            Snapshot();
            var s = _model.FindShape(_editingShapeId);
            if (s != null)
            {
                s.Rtf = ShapeFactory.SaveRich(_richEditBox);
                s.Label = new TextRange(_richEditBox.Document.ContentStart, _richEditBox.Document.ContentEnd).Text.Trim();
                if (_shapeVisuals.TryGetValue(s.Id, out var v)) v.Rebuild();
                ApplySelectionStyles();
            }
            EndAnyEdit();
            return;
        }

        if (_textEditBox == null) return;
        Snapshot();
        var text = _textEditBox.Text;
        if (_editingShapeId != null)
        {
            var s = _model.FindShape(_editingShapeId);
            if (s != null)
            {
                s.Label = text;
                if (_shapeVisuals.TryGetValue(s.Id, out var v)) v.SetLabel(text);
            }
        }
        else if (_editingConnectionId != null)
        {
            var c = _model.Connections.FirstOrDefault(x => x.Id == _editingConnectionId);
            if (c != null)
            {
                c.Label = text;
                if (_connVisuals.TryGetValue(c.Id, out var v)) v.SetLabel(text);
            }
        }
        EndAnyEdit();
    }

    private void EndAnyEdit()
    {
        if (_textEditBox != null && _overlayLayer.Children.Contains(_textEditBox))
            _overlayLayer.Children.Remove(_textEditBox);
        if (_richEditBox != null && _overlayLayer.Children.Contains(_richEditBox))
            _overlayLayer.Children.Remove(_richEditBox);
        _textEditBox = null;
        _richEditBox = null;
        _editingShapeId = null;
        _editingConnectionId = null;
        _isEditingText = false;
        RebuildOverlay();
    }

    // ---------- Helpers ----------

    private Point ScreenToWorld(Point screen)
    {
        var inv = _worldTransform.Matrix;
        inv.Invert();
        return inv.Transform(screen);
    }

    private ShapeNode? HitTestShape(Point world)
    {
        // Iterate from top z-order down
        foreach (var s in _model.Shapes.OrderByDescending(s => s.ZIndex))
        {
            if (world.X >= s.X && world.X <= s.X + s.Width
             && world.Y >= s.Y && world.Y <= s.Y + s.Height)
                return s;
        }
        return null;
    }

    private string? HitTestConnection(Point world)
    {
        var pen = new Pen(Brushes.Black, 16);
        foreach (var (id, v) in _connVisuals)
            if (v.HitPath.Data != null && v.HitPath.Data.StrokeContains(pen, world))
                return id;
        return null;
    }

    private Guide? HitTestGuide(Point world)
    {
        double thresh = 5.0 / Math.Max(Zoom, 0.0001);
        Guide? best = null;
        double bestD = thresh;
        foreach (var g in _model.Guides)
        {
            double d = g.Horizontal ? Math.Abs(world.Y - g.Position) : Math.Abs(world.X - g.Position);
            if (d < bestD) { bestD = d; best = g; }
        }
        return best;
    }

    // Snap the moving selection's edges/centers to other shapes and guides.
    private (double dx, double dy) ComputeMoveSnap(double dx, double dy)
    {
        _snapX = null; _snapY = null;
        if (_dragOrigins.Count == 0 || !AppSettings.Current.SnapEnabled) return (dx, dy);

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (id, origin) in _dragOrigins)
        {
            var s = _model.FindShape(id); if (s == null) continue;
            var x = origin.X + dx; var y = origin.Y + dy;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + s.Width); maxY = Math.Max(maxY, y + s.Height);
        }
        if (minX == double.MaxValue) return (dx, dy);

        double cX = (minX + maxX) / 2, cY = (minY + maxY) / 2;
        var movingXs = new[] { minX, cX, maxX };
        var movingYs = new[] { minY, cY, maxY };

        double thresh = 6.0 / Math.Max(Zoom, 0.0001);
        double bestX = thresh, bestY = thresh, adjX = 0, adjY = 0;
        double? snapX = null, snapY = null;

        void ConsiderX(double target)
        {
            foreach (var m in movingXs) { var d = Math.Abs(m - target); if (d < bestX) { bestX = d; adjX = target - m; snapX = target; } }
        }
        void ConsiderY(double target)
        {
            foreach (var m in movingYs) { var d = Math.Abs(m - target); if (d < bestY) { bestY = d; adjY = target - m; snapY = target; } }
        }

        foreach (var s in _model.Shapes)
        {
            if (_dragOrigins.ContainsKey(s.Id)) continue;
            ConsiderX(s.X); ConsiderX(s.X + s.Width / 2); ConsiderX(s.X + s.Width);
            ConsiderY(s.Y); ConsiderY(s.Y + s.Height / 2); ConsiderY(s.Y + s.Height);
        }
        foreach (var g in _model.Guides)
        {
            if (g.Horizontal) ConsiderY(g.Position); else ConsiderX(g.Position);
        }

        _snapX = snapX; _snapY = snapY;
        return (dx + adjX, dy + adjY);
    }

    private void DrawSnapGuides()
    {
        if (_snapX == null && _snapY == null) return;
        var tl = ScreenToWorld(new Point(0, 0));
        var br = ScreenToWorld(new Point(ActualWidth, ActualHeight));
        var color = (Brush)new BrushConverter().ConvertFromString("#EF4444")!;
        double t = 1.0 / Math.Max(Zoom, 0.4);
        if (_snapX is double sx)
            _overlayLayer.Children.Add(new Line { X1 = sx, Y1 = tl.Y, X2 = sx, Y2 = br.Y, Stroke = color, StrokeThickness = t, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }) });
        if (_snapY is double sy)
            _overlayLayer.Children.Add(new Line { X1 = tl.X, Y1 = sy, X2 = br.X, Y2 = sy, Stroke = color, StrokeThickness = t, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }) });
    }

    private void UpdateCursor()
    {
        if (_spaceHeld || _tool == ToolMode.Pan) Cursor = Cursors.SizeAll;
        else if (_tool == ToolMode.Select) Cursor = Cursors.Arrow;
        else Cursor = Cursors.Cross;
    }

    public void SetZoom(double zoom)
    {
        zoom = Math.Max(0.2, Math.Min(4.0, zoom));
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var oldZoom = _worldTransform.Matrix.M11;
        var scale = zoom / oldZoom;
        var m = _worldTransform.Matrix;
        m.ScaleAt(scale, scale, center.X, center.Y);
        _worldTransform.Matrix = m;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetView()
    {
        _worldTransform.Matrix = Matrix.Identity;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== Presentation screens & view framing =====

    private const double FreeRegionW = 1000;
    private const double FreeRegionH = 620;
    private const double FreeRegionGap = 140;

    /// Finds an empty gap (clear of shapes and existing screens), pans to it, and marks it
    /// as the next numbered presentation screen. Returns the new frame so the UI can refresh.
    public PresentationFrame GoToFreeSpace()
    {
        ClearSelection();
        var region = FindFreeRegion(new Size(FreeRegionW, FreeRegionH), FreeRegionGap);
        var frame = AddFrame(region);
        ShowRect(region, animate: true);
        return frame;
    }

    /// Searches outward (nearest-first) for a region of the given size that overlaps no
    /// shape and no existing screen. Falls back to the right of all content if none is found.
    public Rect FindFreeRegion(Size size, double gap)
    {
        double w = size.Width, h = size.Height;
        double stepX = w + gap, stepY = h + gap;

        var obstacles = new List<Rect>();
        foreach (var s in _model.Shapes) obstacles.Add(new Rect(s.X, s.Y, s.Width, s.Height));
        foreach (var f in _model.Frames) obstacles.Add(new Rect(f.X, f.Y, f.Width, f.Height));

        // Origin: top-left of existing content, else the current view's top-left.
        double ox, oy;
        if (obstacles.Count > 0)
        {
            ox = obstacles.Min(r => r.X);
            oy = obstacles.Min(r => r.Y);
        }
        else
        {
            var tl = ScreenToWorld(new Point(0, 0));
            ox = tl.X + 40; oy = tl.Y + 40;
            return new Rect(ox, oy, w, h);
        }

        bool Free(Rect cand)
        {
            var padded = new Rect(cand.X - gap / 2, cand.Y - gap / 2, cand.Width + gap, cand.Height + gap);
            foreach (var o in obstacles) if (padded.IntersectsWith(o)) return false;
            return true;
        }

        // Expanding square spiral over a grid of cells, nearest ring first.
        for (int ring = 0; ring <= 80; ring++)
        {
            for (int gi = -ring; gi <= ring; gi++)
            for (int gj = -ring; gj <= ring; gj++)
            {
                if (Math.Max(Math.Abs(gi), Math.Abs(gj)) != ring) continue; // perimeter only
                var cand = new Rect(ox + gi * stepX, oy + gj * stepY, w, h);
                if (Free(cand)) return cand;
            }
        }

        // Fallback: just to the right of everything.
        double maxX = obstacles.Max(r => r.Right);
        return new Rect(maxX + gap, oy, w, h);
    }

    /// Merges another model's shapes + connections into this one, placed in a free area
    /// with fresh IDs (so it doesn't overlap existing content), then pans to it. Used to
    /// add a template to the current document.
    public void InsertModel(DiagramModel src)
    {
        if (src.Shapes.Count == 0) return;
        Snapshot();

        double minX = src.Shapes.Min(s => s.X), minY = src.Shapes.Min(s => s.Y);
        double maxX = src.Shapes.Max(s => s.X + s.Width), maxY = src.Shapes.Max(s => s.Y + s.Height);
        double w = Math.Max(50, maxX - minX), h = Math.Max(50, maxY - minY);
        var region = FindFreeRegion(new Size(w, h), 100);
        double dx = region.X - minX, dy = region.Y - minY;

        var idMap = new Dictionary<string, string>();
        var newIds = new List<string>();
        foreach (var s in src.Shapes)
        {
            var copy = new ShapeNode
            {
                Kind = s.Kind, X = s.X + dx, Y = s.Y + dy, Width = s.Width, Height = s.Height,
                Label = s.Label, Fill = s.Fill, Stroke = s.Stroke, Stencil = s.Stencil, Image = s.Image, Rtf = s.Rtf,
                FontFamily = s.FontFamily, FontSize = s.FontSize, Bold = s.Bold, Italic = s.Italic,
                Underline = s.Underline, FontColor = s.FontColor, TextAlign = s.TextAlign,
                ZIndex = _nextZ++
            };
            idMap[s.Id] = copy.Id;
            _model.Shapes.Add(copy);
            AddShapeVisual(copy);
            newIds.Add(copy.Id);
        }
        foreach (var c in src.Connections)
        {
            if (!idMap.TryGetValue(c.FromId, out var fid) || !idMap.TryGetValue(c.ToId, out var tid)) continue;
            var conn = new Connection
            {
                FromId = fid, ToId = tid, Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed,
                Routing = c.Routing, StrokeStyle = c.StrokeStyle, CurveDX = c.CurveDX, CurveDY = c.CurveDY
            };
            _model.Connections.Add(conn);
            AddConnectionVisual(conn);
        }

        _selected.Clear();
        _selectedConnectionId = null;
        foreach (var id in newIds) _selected.Add(id);
        ApplySelectionStyles();
        RebuildOverlay();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ShowRect(region, animate: true);
    }

    // ---- presentation frame model mutators (snapshot for undo) ----
    public IReadOnlyList<PresentationFrame> Frames => _model.Frames;

    public PresentationFrame AddFrame(Rect r)
    {
        Snapshot();
        var f = new PresentationFrame { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
        _model.Frames.Add(f);
        Renumber();
        return f;
    }

    public void RemoveFrame(string id)
    {
        var f = _model.Frames.FirstOrDefault(x => x.Id == id);
        if (f == null) return;
        Snapshot();
        _model.Frames.Remove(f);
        Renumber();
    }

    /// Moves a screen earlier (dir<0) or later (dir>0) in the running order.
    public void MoveFrame(string id, int dir)
    {
        var ordered = _model.Frames.OrderBy(f => f.Order).ToList();
        var idx = ordered.FindIndex(f => f.Id == id);
        var j = idx + (dir < 0 ? -1 : 1);
        if (idx < 0 || j < 0 || j >= ordered.Count) return;
        Snapshot();
        (ordered[idx], ordered[j]) = (ordered[j], ordered[idx]);
        for (int i = 0; i < ordered.Count; i++) ordered[i].Order = i + 1;
    }

    /// Re-captures a screen's region (position + zoom) from a world rect — typically the
    /// current view, so the user frames an area then applies it to a stored screen.
    public void UpdateFrame(string id, Rect r)
    {
        var f = _model.Frames.FirstOrDefault(x => x.Id == id);
        if (f == null) return;
        Snapshot();
        f.X = r.X; f.Y = r.Y; f.Width = r.Width; f.Height = r.Height;
    }

    public void RenameFrame(string id, string? name)
    {
        var f = _model.Frames.FirstOrDefault(x => x.Id == id);
        if (f == null) return;
        Snapshot();
        f.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private void Renumber()
    {
        var ordered = _model.Frames.OrderBy(f => f.Order).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Order = i + 1;
    }

    // ---- view framing & animation ----
    public Point ViewCenterWorld() => ScreenToWorld(new Point(ActualWidth / 2, ActualHeight / 2));

    /// The world-space rectangle currently visible in the viewport.
    public Rect CurrentViewWorldRect()
    {
        var tl = ScreenToWorld(new Point(0, 0));
        var size = GetViewportWorldSize();
        return new Rect(tl.X, tl.Y, Math.Max(1, size.Width), Math.Max(1, size.Height));
    }

    /// Sets an absolute view (zoom + world center). Unlike SetZoom this isn't clamped to the
    /// editing zoom range, so presentation overviews can pull way out.
    public void SetView(double zoom, Point centerWorld)
    {
        zoom = Math.Max(0.0008, Math.Min(12.0, zoom));
        var m = _worldTransform.Matrix;
        m.M11 = zoom; m.M22 = zoom; m.M12 = 0; m.M21 = 0;
        m.OffsetX = ActualWidth / 2 - centerWorld.X * zoom;
        m.OffsetY = ActualHeight / 2 - centerWorld.Y * zoom;
        _worldTransform.Matrix = m;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private double ZoomToFit(Rect r, double pad = 0.92)
    {
        if (r.Width <= 0 || r.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0) return Zoom;
        return Math.Min(ActualWidth / r.Width, ActualHeight / r.Height) * pad;
    }

    /// Frames a world rect in the viewport. When animating, a quick direct tween (no
    /// presentation fly-out) — used while editing (Free Space, screen preview clicks).
    public void ShowRect(Rect r, bool animate)
    {
        var zEnd = ZoomToFit(r);
        var cEnd = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        if (!animate) { StopViewAnimation(); SetView(zEnd, cEnd); return; }
        AnimateViewDirect(zEnd, cEnd, 0.6, null);
    }

    /// Single-phase eased tween straight from the current view to (zEnd, cEnd).
    public void AnimateViewDirect(double zEnd, Point cEnd, double seconds, Action? onDone)
    {
        StopViewAnimation();
        double zStart = Zoom;
        Point cStart = ViewCenterWorld();
        _viewAnimStarted = false;

        static double Ease(double u) => u * u * (3 - 2 * u);

        _viewAnim = (s, e) =>
        {
            var rt = ((System.Windows.Media.RenderingEventArgs)e).RenderingTime;
            if (!_viewAnimStarted) { _viewAnimStart = rt; _viewAnimStarted = true; }
            double t = (rt - _viewAnimStart).TotalSeconds / seconds;
            if (t >= 1) t = 1;
            double u = Ease(t);
            double z = zStart * Math.Pow(zEnd / zStart, u);
            double cx = cStart.X + (cEnd.X - cStart.X) * u;
            double cy = cStart.Y + (cEnd.Y - cStart.Y) * u;
            SetView(z, new Point(cx, cy));
            if (t >= 1) { StopViewAnimation(); onDone?.Invoke(); }
        };
        CompositionTarget.Rendering += _viewAnim;
    }

    /// The "zoom way out" waypoint: the union of all screens (or all content), padded.
    public Rect OverviewRect()
    {
        var rects = new List<Rect>();
        foreach (var f in _model.Frames) rects.Add(new Rect(f.X, f.Y, f.Width, f.Height));
        if (rects.Count == 0)
            foreach (var s in _model.Shapes) rects.Add(new Rect(s.X, s.Y, s.Width, s.Height));
        if (rects.Count == 0) return new Rect(-500, -500, 1000, 1000);
        var u = rects.Aggregate(Rect.Union);
        u.Inflate(u.Width * 0.12 + 80, u.Height * 0.12 + 80);
        return u;
    }

    // CompositionTarget.Rendering-driven view tween: current view -> overview -> target.
    private EventHandler? _viewAnim;
    private TimeSpan _viewAnimStart;
    private bool _viewAnimStarted;

    public bool IsAnimatingView => _viewAnim != null;

    /// Animates to (zEnd, cEnd) via an overview waypoint (fit to overviewRect), over `seconds`.
    public void AnimateView(double zEnd, Point cEnd, Rect overviewRect, double seconds, Action? onDone)
    {
        StopViewAnimation();
        double zStart = Zoom;
        Point cStart = ViewCenterWorld();
        double zMid = ZoomToFit(overviewRect, 0.9);
        // Only treat it as a fly-out if the overview is actually further out than both ends.
        zMid = Math.Min(zMid, Math.Min(zStart, zEnd));
        Point cMid = new(overviewRect.X + overviewRect.Width / 2, overviewRect.Y + overviewRect.Height / 2);
        _viewAnimStarted = false;

        static double Ease(double u) => u * u * (3 - 2 * u);
        static double GeoLerp(double a, double b, double u) => a * Math.Pow(b / a, u);
        static double Lerp(double a, double b, double u) => a + (b - a) * u;

        _viewAnim = (s, e) =>
        {
            var rt = ((System.Windows.Media.RenderingEventArgs)e).RenderingTime;
            if (!_viewAnimStarted) { _viewAnimStart = rt; _viewAnimStarted = true; }
            double t = (rt - _viewAnimStart).TotalSeconds / seconds;
            if (t >= 1) t = 1;

            double z, cx, cy;
            if (t < 0.5)
            {
                double u = Ease(t / 0.5);
                z = GeoLerp(zStart, zMid, u);
                cx = Lerp(cStart.X, cMid.X, u); cy = Lerp(cStart.Y, cMid.Y, u);
            }
            else
            {
                double u = Ease((t - 0.5) / 0.5);
                z = GeoLerp(zMid, zEnd, u);
                cx = Lerp(cMid.X, cEnd.X, u); cy = Lerp(cMid.Y, cEnd.Y, u);
            }
            SetView(z, new Point(cx, cy));
            if (t >= 1) { StopViewAnimation(); onDone?.Invoke(); }
        };
        CompositionTarget.Rendering += _viewAnim;
    }

    public void StopViewAnimation()
    {
        if (_viewAnim != null) { CompositionTarget.Rendering -= _viewAnim; _viewAnim = null; }
    }

    // ---- presentation visual mode (background + grid + read-only) ----
    private Matrix _savedViewMatrix = Matrix.Identity;

    public void BeginPresentationVisual(Color background)
    {
        _savedViewMatrix = _worldTransform.Matrix;
        ReadOnly = true;
        Background = new SolidColorBrush(background);
    }

    /// Live-updates the presentation background while presenting (no-op otherwise).
    public void SetPresentationBackground(Color background)
    {
        if (ReadOnly) Background = new SolidColorBrush(background);
    }

    public void EndPresentationVisual()
    {
        StopViewAnimation();
        _world.BeginAnimation(UIElement.OpacityProperty, null);
        _world.Opacity = 1;
        _rotate.Angle = 0;
        ReadOnly = false;
        Background = _gridBrush;
        _worldTransform.Matrix = _savedViewMatrix;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// Animates to a presentation frame (numbered screen) with the overview fly-out.
    public void PresentGoToFrame(PresentationFrame f, double seconds, Action? onDone)
    {
        var r = new Rect(f.X, f.Y, f.Width, f.Height);
        AnimateView(ZoomToFit(r, 0.96), new Point(r.X + r.Width / 2, r.Y + r.Height / 2),
                    OverviewRect(), seconds, onDone);
    }

    /// Transitions to a frame using the chosen style: "zoom" (overview fly-out), "glide"
    /// (direct pan+zoom), "cut" (instant), or "fade" (dip through the background).
    public void PresentTransitionTo(PresentationFrame f, string? style, Action? onDone)
    {
        var r = new Rect(f.X, f.Y, f.Width, f.Height);
        var zEnd = ZoomToFit(r, 0.96);
        var cEnd = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        _rotate.Angle = 0; // clear any leftover rotation from an interrupted spin
        switch (style)
        {
            case "cut":
                StopViewAnimation(); SetView(zEnd, cEnd); onDone?.Invoke();
                break;
            case "glide":
                AnimateViewDirect(zEnd, cEnd, 1.1, onDone);
                break;
            case "fade":
                FadeTransition(zEnd, cEnd, onDone);
                break;
            case "spin":
                SpinTransition(zEnd, cEnd, onDone);
                break;
            case "spring":
                SpringTransition(zEnd, cEnd, onDone);
                break;
            case "panzoom":
                PanThenZoomTransition(zEnd, cEnd, onDone);
                break;
            case "whirl":
                WhirlwindTransition(zEnd, cEnd, r.Width, r.Height, onDone);
                break;
            default: // "zoom"
                AnimateView(zEnd, cEnd, OverviewRect(), 2.4, onDone);
                break;
        }
    }

    private static double Smooth(double u) => u * u * (3 - 2 * u);

    // Runs a per-frame view tween over `seconds`; apply(t) sets the view for progress t in [0,1].
    private void RunViewTween(double seconds, Action<double> apply, Action? onDone)
    {
        StopViewAnimation();
        _viewAnimStarted = false;
        _viewAnim = (s, e) =>
        {
            var rt = ((System.Windows.Media.RenderingEventArgs)e).RenderingTime;
            if (!_viewAnimStarted) { _viewAnimStart = rt; _viewAnimStarted = true; }
            double t = (rt - _viewAnimStart).TotalSeconds / seconds;
            if (t >= 1) t = 1;
            apply(t);
            if (t >= 1) { StopViewAnimation(); onDone?.Invoke(); }
        };
        CompositionTarget.Rendering += _viewAnim;
    }

    // Rotate & zoom: a slight rotation that unwinds as the view flies to the target.
    private void SpinTransition(double zEnd, Point cEnd, Action? onDone)
    {
        double zStart = Zoom; Point cStart = ViewCenterWorld();
        _rotate.CenterX = ActualWidth / 2; _rotate.CenterY = ActualHeight / 2;
        const double startAngle = 12;
        RunViewTween(1.4, t =>
        {
            double u = Smooth(t);
            _rotate.Angle = startAngle * (1 - u);
            SetView(zStart * Math.Pow(zEnd / zStart, u),
                    new Point(cStart.X + (cEnd.X - cStart.X) * u, cStart.Y + (cEnd.Y - cStart.Y) * u));
        }, () => { _rotate.Angle = 0; onDone?.Invoke(); });
    }

    // Spring zoom: overshoots the target slightly, then settles (back-ease).
    private void SpringTransition(double zEnd, Point cEnd, Action? onDone)
    {
        double zStart = Zoom; Point cStart = ViewCenterWorld();
        const double c1 = 1.70158, c3 = c1 + 1;
        RunViewTween(1.2, t =>
        {
            double b = t - 1;
            double u = 1 + c3 * b * b * b + c1 * b * b; // BackEase-out (overshoots near the end)
            SetView(zStart * Math.Pow(zEnd / zStart, u),
                    new Point(cStart.X + (cEnd.X - cStart.X) * u, cStart.Y + (cEnd.Y - cStart.Y) * u));
        }, onDone);
    }

    // Whirlwind: zooms out then back in (like the overview fly-out) while the focal point
    // spirals inward in a decaying circle and the world unwinds a couple of turns — so it
    // appears to swirl around before landing on the target.
    private void WhirlwindTransition(double zEnd, Point cEnd, double rW, double rH, Action? onDone)
    {
        double zStart = Zoom; Point cStart = ViewCenterWorld();
        double zMid = Math.Min(zStart, zEnd) * 0.5;                 // pull out partway
        double dx = cEnd.X - cStart.X, dy = cEnd.Y - cStart.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double radius = Math.Max(dist * 0.30, Math.Max(rW, rH) * 0.6); // orbit size (world units)
        _rotate.CenterX = ActualWidth / 2; _rotate.CenterY = ActualHeight / 2;
        const double turns = 2.5;     // how many times it circles
        const double spinDeg = 540;   // content rotation that unwinds to 0

        RunViewTween(2.6, t =>
        {
            double u = Smooth(t);
            double z = u < 0.5
                ? zStart * Math.Pow(zMid / zStart, u / 0.5)
                : zMid * Math.Pow(zEnd / zMid, (u - 0.5) / 0.5);
            double bx = cStart.X + dx * u, by = cStart.Y + dy * u;  // straight path...
            double theta = u * turns * 2 * Math.PI;                 // ...plus a decaying orbit
            double rad = radius * (1 - u);
            _rotate.Angle = spinDeg * (1 - u);
            SetView(z, new Point(bx + Math.Cos(theta) * rad, by + Math.Sin(theta) * rad));
        }, () => { _rotate.Angle = 0; onDone?.Invoke(); });
    }

    // Pan, then zoom: glide across at the current zoom, then dive into the target.
    private void PanThenZoomTransition(double zEnd, Point cEnd, Action? onDone)
    {
        double zStart = Zoom; Point cStart = ViewCenterWorld();
        RunViewTween(1.5, t =>
        {
            double z, cx, cy;
            if (t < 0.5)
            {
                double u = Smooth(t / 0.5);
                z = zStart; cx = cStart.X + (cEnd.X - cStart.X) * u; cy = cStart.Y + (cEnd.Y - cStart.Y) * u;
            }
            else
            {
                double u = Smooth((t - 0.5) / 0.5);
                z = zStart * Math.Pow(zEnd / zStart, u); cx = cEnd.X; cy = cEnd.Y;
            }
            SetView(z, new Point(cx, cy));
        }, onDone);
    }

    // Fades the content out to the background color, jumps the view, then fades back in.
    private void FadeTransition(double zEnd, Point cEnd, Action? onDone)
    {
        StopViewAnimation();
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var fadeOut = new DoubleAnimation(_world.Opacity, 0, TimeSpan.FromSeconds(0.32)) { EasingFunction = ease };
        fadeOut.Completed += (s, e) =>
        {
            SetView(zEnd, cEnd);
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.32)) { EasingFunction = ease };
            fadeIn.Completed += (s2, e2) => { _world.BeginAnimation(UIElement.OpacityProperty, null); _world.Opacity = 1; onDone?.Invoke(); };
            _world.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        _world.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    // ---- Scrollbar support ----

    public (double X, double Y) GetScrollOffsetWorld()
    {
        var p = ScreenToWorld(new Point(0, 0));
        return (p.X, p.Y);
    }

    public Size GetViewportWorldSize()
    {
        var z = Zoom;
        return z <= 0 ? new Size(0, 0) : new Size(ActualWidth / z, ActualHeight / z);
    }

    public Rect GetContentExtentWorld()
    {
        if (_model.Shapes.Count == 0) return new Rect(-200, -200, 400, 400);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in _model.Shapes)
        {
            minX = Math.Min(minX, s.X); minY = Math.Min(minY, s.Y);
            maxX = Math.Max(maxX, s.X + s.Width); maxY = Math.Max(maxY, s.Y + s.Height);
        }
        const double pad = 200;
        return new Rect(minX - pad, minY - pad, (maxX - minX) + pad * 2, (maxY - minY) + pad * 2);
    }

    public void ScrollViewTo(double worldX, double worldY)
    {
        var m = _worldTransform.Matrix;
        var s = m.M11;
        m.OffsetX = -worldX * s;
        m.OffsetY = -worldY * s;
        _worldTransform.Matrix = m;
        UpdateGridOffset();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Grid pattern ----
    private DrawingBrush _gridBrush = null!;
    private Brush BuildGridBackground()
    {
        var dot = new EllipseGeometry(new Point(2, 2), 1, 1);
        var drawing = new GeometryDrawing(
            (Brush)new BrushConverter().ConvertFromString("#CBD5E1")!,
            null, dot);
        var dg = new DrawingGroup();
        dg.Children.Add(drawing);
        _gridBrush = new DrawingBrush
        {
            Drawing = dg,
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 24, 24)
        };
        return _gridBrush;
    }

    private void UpdateGridOffset()
    {
        var m = _worldTransform.Matrix;
        _gridBrush.Viewport = new Rect(m.OffsetX % 24, m.OffsetY % 24, 24, 24);
    }
}

// ---------------- ShapeVisual ----------------

public class ShapeVisual
{
    public ShapeNode Node;
    public Grid Element;
    private System.Windows.Shapes.Shape[] _styledParts = Array.Empty<System.Windows.Shapes.Shape>();
    private UIElement? _body;
    private TextBlock _label;

    public ShapeVisual(ShapeNode node)
    {
        Node = node;
        _label = new TextBlock
        {
            Text = node.Label,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = (Brush)new BrushConverter().ConvertFromString("#0F172A")!,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 4, 8, 4),
            IsHitTestVisible = false
        };
        Element = new Grid
        {
            Width = node.Width,
            Height = node.Height,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll
        };
    }

    public void Rebuild()
    {
        Element.Children.Clear();
        Element.Width = Node.Width;
        Element.Height = Node.Height;

        var fill = (Brush)new BrushConverter().ConvertFromString(Node.Fill)!;
        var stroke = (Brush)new BrushConverter().ConvertFromString(Node.Stroke)!;
        var (body, styled) = ShapeFactory.BuildBody(Node.Kind, Node.Width, Node.Height, fill, stroke, Node.Stencil, Node.Image, Node.Rtf, Node.Label);
        _body = body;
        _styledParts = styled;
        Element.Children.Add(body);

        // Rich-text shapes render their content inside the body (a read-only RichTextBox);
        // they have no separate single-font label.
        if (Node.Kind == ShapeKind.RichText) return;

        // Special label margins for non-rect shapes (avoid overlapping cylinder top, etc.)
        _label.Text = Node.Label;

        // Typography: explicit per-shape settings win, otherwise fall back to the per-kind
        // defaults that have always applied (tiles use a smaller, semi-bold label).
        _label.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(Node.FontFamily) ? "Segoe UI" : Node.FontFamily);
        _label.FontSize = Node.FontSize ?? (Node.Kind == ShapeKind.ServiceTile ? 11.5 : 13);
        _label.FontWeight = Node.Bold
            ? FontWeights.Bold
            : (Node.Kind == ShapeKind.ServiceTile ? FontWeights.SemiBold : FontWeights.Normal);
        _label.FontStyle = Node.Italic ? FontStyles.Italic : FontStyles.Normal;
        _label.TextDecorations = Node.Underline ? TextDecorations.Underline : null;
        _label.Foreground = (Brush)new BrushConverter().ConvertFromString(
            string.IsNullOrWhiteSpace(Node.FontColor) ? "#0F172A" : Node.FontColor)!;
        _label.TextAlignment = Node.TextAlign switch
        {
            TextAlign.Left  => TextAlignment.Left,
            TextAlign.Right => TextAlignment.Right,
            _               => TextAlignment.Center,
        };
        _label.Margin = Node.Kind switch
        {
            ShapeKind.Cylinder    => new Thickness(8, Math.Min(Node.Height * 0.18, 18), 8, 4),
            ShapeKind.Person      => new Thickness(8, Node.Height * 0.55, 8, 4),
            ShapeKind.Note        => new Thickness(8, 6, Math.Min(Node.Width * 0.22, 22) + 4, 4),
            ShapeKind.ServiceTile => new Thickness(6, Node.Height * 0.52, 6, 4),
            _ => new Thickness(8, 4, 8, 4)
        };
        Element.Children.Add(_label);
    }

    public void SetSelected(bool selected)
    {
        var color = selected
            ? (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!
            : (Brush)new BrushConverter().ConvertFromString(Node.Stroke)!;
        var thickness = selected ? 2.2 : ShapeFactory.StrokeThickness;
        foreach (var p in _styledParts)
        {
            p.Stroke = color;
            p.StrokeThickness = thickness;
        }
    }

    public void SetConnectSource(bool active)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString("#14B8A6")!;
        foreach (var p in _styledParts)
        {
            p.Stroke = active ? brush : (Brush)new BrushConverter().ConvertFromString(Node.Stroke)!;
            p.StrokeDashArray = active ? new DoubleCollection(new[] { 5.0, 3.0 }) : new DoubleCollection();
            p.StrokeThickness = active ? 2.2 : ShapeFactory.StrokeThickness;
        }
    }

    public void SetConnectTarget(bool active)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString("#14B8A6")!;
        foreach (var p in _styledParts)
        {
            p.Stroke = active ? brush : (Brush)new BrushConverter().ConvertFromString(Node.Stroke)!;
            p.StrokeThickness = active ? 2.4 : ShapeFactory.StrokeThickness;
        }
    }

    public void SetLabel(string text)
    {
        _label.Text = text;
        Node.Label = text;
    }
}

// ---------------- ConnectionVisual ----------------

public class ConnectionVisual
{
    public Connection Conn;
    public Path Path;        // the connector line (carries the dash/dot style)
    public Path Arrow;       // solid filled arrowhead (never dashed)
    public Path HitPath;     // wide transparent path for easy clicking
    public Point MidPoint { get; private set; }       // straight midpoint (label anchor)
    public Point ControlPoint { get; private set; }   // curve control point (drag handle)

    private const double ArrowLen = 10, ArrowWidth = 6;

    public ConnectionVisual(Connection conn)
    {
        Conn = conn;
        var stroke = MakeBrush(conn.Stroke);
        Path = new Path
        {
            Stroke = stroke, StrokeThickness = 1.8, Fill = null,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        Arrow = new Path { Stroke = null, Fill = stroke };
        HitPath = new Path { Stroke = Brushes.Transparent, StrokeThickness = 14, Fill = null };
        ApplyStrokeStyle();
    }

    private static Brush MakeBrush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

    private void ApplyStrokeStyle()
    {
        // Legacy files only have the Dashed flag; treat that as Dashed.
        var style = Conn.StrokeStyle == StrokeStyle.Solid && Conn.Dashed ? StrokeStyle.Dashed : Conn.StrokeStyle;
        switch (style)
        {
            case StrokeStyle.Dashed:
                Path.StrokeDashCap = PenLineCap.Flat;
                Path.StrokeDashArray = new DoubleCollection(new[] { 6.0, 4.0 });
                break;
            case StrokeStyle.Dotted:
                Path.StrokeDashCap = PenLineCap.Round;
                Path.StrokeDashArray = new DoubleCollection(new[] { 0.1, 2.5 });
                break;
            default:
                Path.StrokeDashArray = new DoubleCollection();
                break;
        }
    }

    public void SetEndpoints(Point from, Point to)
    {
        ApplyStrokeStyle();
        MidPoint = new Point((from.X + to.X) / 2.0, (from.Y + to.Y) / 2.0);
        switch (Conn.Routing)
        {
            case ConnectorRouting.Curved: BuildCurved(from, to); break;
            case ConnectorRouting.Elbow:  BuildElbow(from, to);  break;
            default:                      BuildStraight(from, to); break;
        }
    }

    private void BuildStraight(Point from, Point to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { Clear(); return; }
        var ux = dx / len; var uy = dy / len;
        ControlPoint = MidPoint;
        Path.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} L {to.X - ux * ArrowLen},{to.Y - uy * ArrowLen}"));
        HitPath.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} L {to.X},{to.Y}"));
        SetArrow(to, ux, uy);
    }

    private void BuildCurved(Point from, Point to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { Clear(); return; }

        Point c;
        if (Conn.CurveDX == 0 && Conn.CurveDY == 0)
        {
            var px = -dy / len; var py = dx / len;       // perpendicular
            var bow = Math.Min(80, len * 0.25);
            c = new Point(MidPoint.X + px * bow, MidPoint.Y + py * bow);
        }
        else c = new Point(MidPoint.X + Conn.CurveDX, MidPoint.Y + Conn.CurveDY);
        ControlPoint = c;

        var tx = to.X - c.X; var ty = to.Y - c.Y;        // tangent at the end
        var tl = Math.Sqrt(tx * tx + ty * ty); if (tl < 1) tl = 1;
        var ux = tx / tl; var uy = ty / tl;
        Path.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} Q {c.X},{c.Y} {to.X - ux * ArrowLen},{to.Y - uy * ArrowLen}"));
        HitPath.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} Q {c.X},{c.Y} {to.X},{to.Y}"));
        SetArrow(to, ux, uy);
    }

    private void BuildElbow(Point from, Point to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y;
        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) { Clear(); return; }
        Point e1, e2; double ux, uy;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var midX = (from.X + to.X) / 2.0;
            e1 = new Point(midX, from.Y); e2 = new Point(midX, to.Y);
            ux = Math.Sign(dx); uy = 0;                  // last leg horizontal
        }
        else
        {
            var midY = (from.Y + to.Y) / 2.0;
            e1 = new Point(from.X, midY); e2 = new Point(to.X, midY);
            ux = 0; uy = Math.Sign(dy);                  // last leg vertical
        }
        ControlPoint = new Point((e1.X + e2.X) / 2.0, (e1.Y + e2.Y) / 2.0);
        Path.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} L {e1.X},{e1.Y} L {e2.X},{e2.Y} L {to.X - ux * ArrowLen},{to.Y - uy * ArrowLen}"));
        HitPath.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} L {e1.X},{e1.Y} L {e2.X},{e2.Y} L {to.X},{to.Y}"));
        SetArrow(to, ux, uy);
    }

    private void SetArrow(Point tip, double ux, double uy)
    {
        var px = -uy; var py = ux;
        var ax1 = tip.X - ux * ArrowLen + px * ArrowWidth;
        var ay1 = tip.Y - uy * ArrowLen + py * ArrowWidth;
        var ax2 = tip.X - ux * ArrowLen - px * ArrowWidth;
        var ay2 = tip.Y - uy * ArrowLen - py * ArrowWidth;
        Arrow.Data = Geometry.Parse(FormattableString.Invariant($"M {ax1},{ay1} L {tip.X},{tip.Y} L {ax2},{ay2} Z"));
        Arrow.Fill = Path.Stroke;
    }

    private void Clear() { Path.Data = HitPath.Data = Arrow.Data = null; }

    public void SetSelected(bool selected)
    {
        var brush = selected ? MakeBrush("#0EA5E9") : MakeBrush(Conn.Stroke);
        Path.Stroke = brush;
        Arrow.Fill = brush;
        Path.StrokeThickness = selected ? 2.4 : 1.8;
    }

    public void SetLabel(string text) => Conn.Label = text;
}

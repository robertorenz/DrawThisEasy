using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly Canvas _overlayLayer = new() { IsHitTestVisible = false };

    private readonly MatrixTransform _worldTransform = new(Matrix.Identity);

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
    private enum DragMode { None, Pan, MoveShape, ResizeShape, ConnectDrag, Marquee }
    private DragMode _drag = DragMode.None;
    private Point _dragStartScreen;
    private Point _dragStartWorld;
    private readonly Dictionary<string, (double X, double Y)> _dragOrigins = new();
    private Matrix _dragStartTransform;
    private string? _resizeShapeId;
    private string _resizeHandle = "";
    private (double X, double Y, double W, double H) _resizeOrigin;
    private string? _connectFromId;
    private Path? _dragLine;
    private Rectangle? _marqueeRect;
    private bool _spaceHeld;
    private bool _isEditingText;
    private bool _rightClickPending;

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

        _world.RenderTransform = _worldTransform;
        _world.IsHitTestVisible = true;
        Children.Add(_world);
        _world.Children.Add(_connLayer);
        _world.Children.Add(_shapeLayer);
        _world.Children.Add(_overlayLayer);

        MouseLeftButtonDown += OnMouseLeftDown;
        MouseLeftButtonUp += OnMouseLeftUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        MouseRightButtonDown += OnMouseRightDown;
        MouseRightButtonUp += OnMouseRightUp;

        // Initial focus when added
        Loaded += (_, _) => Focus();
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
        RebuildOverlay();
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
        var conn = new Connection { FromId = fromId, ToId = toId };
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
            AddShape(addKind.Value, world.X, world.Y);
            // After adding, revert to select tool for fast iteration
            CurrentTool = ToolMode.Select;
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
                // Double-click → edit shape label
                if (e.ClickCount == 2)
                {
                    SelectOnly(hit.Id);
                    BeginEditText(hit);
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
            return;
        }

        if (_drag == DragMode.MoveShape)
        {
            var dx = world.X - _dragStartWorld.X;
            var dy = world.Y - _dragStartWorld.Y;
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
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        var world = ScreenToWorld(e.GetPosition(this));

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
        _dragOrigins.Clear();
        if (IsMouseCaptured) ReleaseMouseCapture();
        UpdateCursor();
    }

    private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
    {
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

        _drag = DragMode.Pan;
        _dragStartScreen = screen;
        _dragStartTransform = _worldTransform.Matrix;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightUp(object sender, MouseButtonEventArgs e)
    {
        if (_rightClickPending)
        {
            _rightClickPending = false;
            var screen = e.GetPosition(this);
            // Only treat as a context-menu click if the pointer barely moved.
            if ((screen - _dragStartScreen).Length < 6)
                ContextMenuRequested?.Invoke(this, screen);
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
        var screen = e.GetPosition(this);
        var oldZoom = _worldTransform.Matrix.M11;
        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        var newZoom = Math.Max(0.2, Math.Min(4.0, oldZoom * factor));
        var scale = newZoom / oldZoom;
        var m = _worldTransform.Matrix;
        m.ScaleAt(scale, scale, screen.X, screen.Y);
        _worldTransform.Matrix = m;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
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
                    Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed
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
        catch (Exception) { return; }

        if (string.IsNullOrWhiteSpace(json)) return;

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
                Label = c.Label, Stroke = c.Stroke, Dashed = c.Dashed
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

    public ShapeNode? GetSelectedShape() => _selected.Count == 1 ? _model.FindShape(_selected.First()) : null;

    // ---------- Text edit ----------

    private TextBox? _textEditBox;
    private string? _editingShapeId;
    private string? _editingConnectionId;

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
        if (!_isEditingText || _textEditBox == null) return;
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
        _textEditBox = null;
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
    }

    public void ResetView()
    {
        _worldTransform.Matrix = Matrix.Identity;
        UpdateGridOffset();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
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
        var (body, styled) = ShapeFactory.BuildBody(Node.Kind, Node.Width, Node.Height, fill, stroke, Node.Stencil);
        _body = body;
        _styledParts = styled;
        Element.Children.Add(body);

        // Special label margins for non-rect shapes (avoid overlapping cylinder top, etc.)
        _label.Text = Node.Label;
        _label.FontSize = Node.Kind == ShapeKind.ServiceTile ? 11.5 : 13;
        _label.FontWeight = Node.Kind == ShapeKind.ServiceTile ? FontWeights.SemiBold : FontWeights.Normal;
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
    public Path Path;        // visible
    public Path HitPath;     // wider transparent for easier clicking
    public Point MidPoint { get; private set; }

    public ConnectionVisual(Connection conn)
    {
        Conn = conn;
        var stroke = (Brush)new BrushConverter().ConvertFromString(conn.Stroke)!;
        Path = new Path
        {
            Stroke = stroke,
            StrokeThickness = 1.8,
            Fill = null,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        if (conn.Dashed) Path.StrokeDashArray = new DoubleCollection(new[] { 6.0, 4.0 });
        HitPath = new Path
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 14,
            Fill = null
        };
    }

    public void SetEndpoints(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { Path.Data = HitPath.Data = null; return; }

        // Shorten end by arrow length so the arrow tip lands at edge
        const double arrowLen = 10;
        const double arrowWidth = 6;
        var ux = dx / len; var uy = dy / len;
        var endX = to.X - ux * arrowLen;
        var endY = to.Y - uy * arrowLen;

        // Slight curve for organic look (offset perpendicular to midpoint)
        var midX = (from.X + endX) / 2.0;
        var midY = (from.Y + endY) / 2.0;
        MidPoint = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);

        // Line + two arrow-head sides. Build with invariant formatting so a comma-decimal
        // locale never injects commas into the geometry string (which breaks Geometry.Parse).
        var px = -uy; var py = ux;
        var ax1 = to.X - ux * arrowLen + px * arrowWidth;
        var ay1 = to.Y - uy * arrowLen + py * arrowWidth;
        var ax2 = to.X - ux * arrowLen - px * arrowWidth;
        var ay2 = to.Y - uy * arrowLen - py * arrowWidth;

        var pathData = FormattableString.Invariant(
            $"M {from.X},{from.Y} L {to.X - ux * arrowLen * 0.05},{to.Y - uy * arrowLen * 0.05} M {ax1},{ay1} L {to.X},{to.Y} L {ax2},{ay2} Z");
        Path.Data = Geometry.Parse(pathData);
        Path.Fill = Path.Stroke;
        HitPath.Data = Geometry.Parse(FormattableString.Invariant($"M {from.X},{from.Y} L {to.X},{to.Y}"));

        UpdateLabel();
    }

    public void SetSelected(bool selected)
    {
        Path.Stroke = selected
            ? (Brush)new BrushConverter().ConvertFromString("#0EA5E9")!
            : (Brush)new BrushConverter().ConvertFromString(Conn.Stroke)!;
        Path.Fill = Path.Stroke;
        Path.StrokeThickness = selected ? 2.4 : 1.8;
    }

    public void SetLabel(string text)
    {
        Conn.Label = text;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        // Cheap label rendering — owner Canvas adds/removes elements.
        // Labels are rendered as part of the connection visual list by the canvas; for simplicity we don't add them here.
    }
}

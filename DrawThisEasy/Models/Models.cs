using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DrawThisEasy.Models;

public enum ShapeKind
{
    Rectangle,
    Rounded,
    Ellipse,
    Diamond,
    Hexagon,
    Parallelogram,
    Cylinder,
    Cloud,
    Server,
    Person,
    Queue,
    Note,
    Text,
    ServiceTile,  // cloud-provider service object; see Stencil + Services/Stencils.cs
    Image,        // raster image; bytes held in ShapeNode.Image as a data URL
    RichText      // formatted text card; content held in ShapeNode.Rtf (RTF), Label is the plain-text fallback
}

public enum ToolMode
{
    Select,
    Connect,
    Pan,
    AddRectangle,
    AddRounded,
    AddEllipse,
    AddDiamond,
    AddHexagon,
    AddParallelogram,
    AddCylinder,
    AddCloud,
    AddServer,
    AddPerson,
    AddQueue,
    AddNote,
    AddText,
    AddRichText
}

public static class ToolModeMap
{
    public static ShapeKind? ShapeForTool(ToolMode mode) => mode switch
    {
        ToolMode.AddRectangle     => ShapeKind.Rectangle,
        ToolMode.AddRounded       => ShapeKind.Rounded,
        ToolMode.AddEllipse       => ShapeKind.Ellipse,
        ToolMode.AddDiamond       => ShapeKind.Diamond,
        ToolMode.AddHexagon       => ShapeKind.Hexagon,
        ToolMode.AddParallelogram => ShapeKind.Parallelogram,
        ToolMode.AddCylinder      => ShapeKind.Cylinder,
        ToolMode.AddCloud         => ShapeKind.Cloud,
        ToolMode.AddServer        => ShapeKind.Server,
        ToolMode.AddPerson        => ShapeKind.Person,
        ToolMode.AddQueue         => ShapeKind.Queue,
        ToolMode.AddNote          => ShapeKind.Note,
        ToolMode.AddText          => ShapeKind.Text,
        ToolMode.AddRichText      => ShapeKind.RichText,
        _ => null
    };
}

public class ShapeNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 140;
    public double Height { get; set; } = 70;
    public string Label { get; set; } = "";
    public string Fill { get; set; } = "#FFFFFF";
    public string Stroke { get; set; } = "#334155";
    public int ZIndex { get; set; }

    // ---- Label typography ----
    // Null / false members fall back to the per-kind defaults in ShapeVisual, so files
    // saved before these fields existed render exactly as they did before.
    /// Font family name (e.g. "Segoe UI"). Null = per-kind default.
    public string? FontFamily { get; set; }
    /// Point size of the label. Null = per-kind default (11.5 for tiles, else 13).
    public double? FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    /// Label text color as hex. Null = default ink (#0F172A).
    public string? FontColor { get; set; }
    public TextAlign TextAlign { get; set; } = TextAlign.Center;

    /// For ShapeKind.ServiceTile: the cloud-service stencil id (e.g. "aws-lambda"). Null otherwise.
    public string? Stencil { get; set; }

    /// For ShapeKind.Image: the image as a data URL ("data:image/png;base64,..."). Null otherwise.
    public string? Image { get; set; }

    /// For ShapeKind.RichText: the formatted content as an RTF string. When null, the plain
    /// Label is shown instead (and becomes the seed text the first time the shape is edited).
    public string? Rtf { get; set; }

    [JsonIgnore]
    public double CenterX => X + Width / 2.0;
    [JsonIgnore]
    public double CenterY => Y + Height / 2.0;
}

// Center is first so default(TextAlign) == Center — matches the historical (and only)
// behavior, so labels in files saved before this field existed stay centered.
public enum TextAlign { Center, Left, Right }

public enum ConnectorRouting { Straight, Curved, Elbow }

public enum StrokeStyle { Solid, Dashed, Dotted }

public class Connection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Stroke { get; set; } = "#334155";
    public bool Dashed { get; set; }   // legacy; superseded by StrokeStyle

    public ConnectorRouting Routing { get; set; } = ConnectorRouting.Straight;
    public StrokeStyle StrokeStyle { get; set; } = StrokeStyle.Solid;

    /// For Curved routing: control-point offset from the straight midpoint (world units).
    /// (0,0) means "auto" — a gentle perpendicular bow.
    public double CurveDX { get; set; }
    public double CurveDY { get; set; }
}

/// A ruler guide line. Horizontal guides sit at a Y position; vertical guides at an X.
public class Guide
{
    public bool Horizontal { get; set; }
    public double Position { get; set; }
}

/// One numbered "screen" in a presentation: a world-space rectangle the view flies to.
public class PresentationFrame
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Order { get; set; }
    /// User-given name; null falls back to "Screen {Order}".
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class DiagramModel
{
    public string Version { get; set; } = "1.0";
    public string Title { get; set; } = "Untitled diagram";
    public List<ShapeNode> Shapes { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<Guide> Guides { get; set; } = new();

    /// Ordered presentation screens (see PresentationFrame). Empty unless the user marks any.
    public List<PresentationFrame> Frames { get; set; } = new();
    /// Background color (hex) used while presenting. Null = white.
    public string? PresentBackground { get; set; }
    /// Transition style between screens: "zoom" (default), "glide", "cut", or "fade".
    public string? PresentTransition { get; set; }

    public ShapeNode? FindShape(string id) => Shapes.Find(s => s.Id == id);
}

public static class Palette
{
    // Fill palette (no purple)
    public static readonly string[] Fills = new[]
    {
        "#FFFFFF", // white
        "#F1F5F9", // slate-100
        "#DBEAFE", // sky/blue-100
        "#CFFAFE", // cyan-100
        "#CCFBF1", // teal-100
        "#D1FAE5", // green-100
        "#FEF3C7", // amber-100
        "#FFE4E6", // rose-100
        "#FFEDD5", // orange-100
    };

    // Stroke palette
    public static readonly string[] Strokes = new[]
    {
        "#334155", // slate-700 (default)
        "#0F172A", // slate-900
        "#0EA5E9", // sky-500
        "#14B8A6", // teal-500
        "#10B981", // emerald-500
        "#F59E0B", // amber-500
        "#EF4444", // red-500
        "#475569", // slate-600
    };
}

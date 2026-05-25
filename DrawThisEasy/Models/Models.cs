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
    Text
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
    AddText
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

    [JsonIgnore]
    public double CenterX => X + Width / 2.0;
    [JsonIgnore]
    public double CenterY => Y + Height / 2.0;
}

public class Connection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Stroke { get; set; } = "#334155";
    public bool Dashed { get; set; }
}

public class DiagramModel
{
    public string Version { get; set; } = "1.0";
    public string Title { get; set; } = "Untitled diagram";
    public List<ShapeNode> Shapes { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();

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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services;

/// Exports a DiagramModel to common interchange formats so diagrams can be opened
/// in other tools: Mermaid (text), draw.io / diagrams.net (mxGraph XML), and Excalidraw (JSON).
public static class DiagramExport
{
    private static string F(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    // ============================ Mermaid ============================

    public static string ToMermaid(DiagramModel m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        var ids = new Dictionary<string, string>();
        int i = 0;
        foreach (var s in m.Shapes)
        {
            var nid = "n" + i++;
            ids[s.Id] = nid;
            var label = MermaidText(string.IsNullOrWhiteSpace(s.Label) ? s.Kind.ToString() : s.Label);
            sb.AppendLine("    " + nid + MermaidShape(s.Kind, label));
        }

        foreach (var c in m.Connections)
        {
            if (!ids.TryGetValue(c.FromId, out var a) || !ids.TryGetValue(c.ToId, out var b)) continue;
            var arrow = c.Dashed ? "-.->" : "-->";
            var lbl = string.IsNullOrWhiteSpace(c.Label) ? "" : "|\"" + MermaidText(c.Label) + "\"|";
            sb.AppendLine($"    {a} {arrow}{lbl} {b}");
        }
        return sb.ToString();
    }

    private static string MermaidShape(ShapeKind k, string label) => k switch
    {
        ShapeKind.Rounded       => $"(\"{label}\")",
        ShapeKind.Ellipse       => $"((\"{label}\"))",
        ShapeKind.Diamond       => $"{{\"{label}\"}}",
        ShapeKind.Hexagon       => $"{{{{\"{label}\"}}}}",
        ShapeKind.Parallelogram => $"[/\"{label}\"/]",
        ShapeKind.Cylinder      => $"[(\"{label}\")]",
        _                       => $"[\"{label}\"]",
    };

    private static string MermaidText(string s) =>
        s.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ").Trim();

    // ============================ draw.io ============================

    public static string ToDrawio(DiagramModel m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<mxGraphModel dx=\"1200\" dy=\"800\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"1100\" pageHeight=\"850\" math=\"0\" shadow=\"0\">");
        sb.AppendLine("  <root>");
        sb.AppendLine("    <mxCell id=\"0\" />");
        sb.AppendLine("    <mxCell id=\"1\" parent=\"0\" />");

        var ids = new Dictionary<string, string>();
        int i = 0;
        foreach (var s in m.Shapes)
        {
            var id = "s" + i++;
            ids[s.Id] = id;
            var style = DrawioStyle(s.Kind) + $"fillColor={s.Fill};strokeColor={s.Stroke};";
            sb.AppendLine(
                $"    <mxCell id=\"{id}\" value=\"{XmlEscape(s.Label)}\" style=\"{style}\" vertex=\"1\" parent=\"1\">");
            sb.AppendLine(
                $"      <mxGeometry x=\"{F(s.X)}\" y=\"{F(s.Y)}\" width=\"{F(s.Width)}\" height=\"{F(s.Height)}\" as=\"geometry\" />");
            sb.AppendLine("    </mxCell>");
        }

        int e = 0;
        foreach (var c in m.Connections)
        {
            if (!ids.TryGetValue(c.FromId, out var src) || !ids.TryGetValue(c.ToId, out var dst)) continue;
            var dashed = c.Dashed ? "dashed=1;" : "dashed=0;";
            var style = $"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;endArrow=classic;{dashed}strokeColor={c.Stroke};";
            sb.AppendLine(
                $"    <mxCell id=\"e{e++}\" value=\"{XmlEscape(c.Label)}\" style=\"{style}\" edge=\"1\" parent=\"1\" source=\"{src}\" target=\"{dst}\">");
            sb.AppendLine("      <mxGeometry relative=\"1\" as=\"geometry\" />");
            sb.AppendLine("    </mxCell>");
        }

        sb.AppendLine("  </root>");
        sb.AppendLine("</mxGraphModel>");
        return sb.ToString();
    }

    private static string DrawioStyle(ShapeKind k) => k switch
    {
        ShapeKind.Rounded       => "rounded=1;whiteSpace=wrap;html=1;",
        ShapeKind.Ellipse       => "ellipse;whiteSpace=wrap;html=1;",
        ShapeKind.Diamond       => "rhombus;whiteSpace=wrap;html=1;",
        ShapeKind.Hexagon       => "shape=hexagon;whiteSpace=wrap;html=1;",
        ShapeKind.Parallelogram => "shape=parallelogram;perimeter=parallelogramPerimeter;whiteSpace=wrap;html=1;",
        ShapeKind.Cylinder      => "shape=cylinder3;whiteSpace=wrap;html=1;boundedLbl=1;",
        ShapeKind.Cloud         => "ellipse;shape=cloud;whiteSpace=wrap;html=1;",
        ShapeKind.Person        => "shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;",
        ShapeKind.Note          => "shape=note;whiteSpace=wrap;html=1;",
        ShapeKind.ServiceTile   => "rounded=1;whiteSpace=wrap;html=1;",
        ShapeKind.Text          => "text;html=1;align=center;verticalAlign=middle;",
        _                       => "rounded=0;whiteSpace=wrap;html=1;",
    };

    private static string XmlEscape(string s) => string.IsNullOrEmpty(s) ? "" : s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("\n", "&#10;");

    // ============================ Excalidraw ============================

    public static string ToExcalidraw(DiagramModel m)
    {
        var elements = new List<object>();
        var rnd = new Random();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var s in m.Shapes)
        {
            elements.Add(ExcalidrawShape(s, rnd, now));
            if (!string.IsNullOrWhiteSpace(s.Label))
                elements.Add(ExcalidrawLabel(s, rnd, now));
        }
        foreach (var c in m.Connections)
        {
            var from = m.FindShape(c.FromId);
            var to = m.FindShape(c.ToId);
            if (from == null || to == null) continue;
            elements.Add(ExcalidrawArrow(from, to, c, rnd, now));
        }

        var doc = new Dictionary<string, object?>
        {
            ["type"] = "excalidraw",
            ["version"] = 2,
            ["source"] = "DrawThisEasy",
            ["elements"] = elements,
            ["appState"] = new Dictionary<string, object?>
            {
                ["gridSize"] = null,
                ["viewBackgroundColor"] = "#ffffff",
            },
            ["files"] = new Dictionary<string, object?>(),
        };

        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ExcalidrawType(ShapeKind k) => k switch
    {
        ShapeKind.Ellipse => "ellipse",
        ShapeKind.Diamond => "diamond",
        ShapeKind.Text    => "text",
        _                 => "rectangle",
    };

    private static Dictionary<string, object?> ExcalidrawBase(string id, string type, double x, double y, double w, double h, Random rnd, long now)
        => new()
        {
            ["id"] = id,
            ["type"] = type,
            ["x"] = x,
            ["y"] = y,
            ["width"] = w,
            ["height"] = h,
            ["angle"] = 0,
            ["strokeColor"] = "#1e1e1e",
            ["backgroundColor"] = "transparent",
            ["fillStyle"] = "solid",
            ["strokeWidth"] = 2,
            ["strokeStyle"] = "solid",
            ["roughness"] = 1,
            ["opacity"] = 100,
            ["groupIds"] = new List<object>(),
            ["frameId"] = null,
            ["roundness"] = null,
            ["seed"] = rnd.Next(1, int.MaxValue),
            ["version"] = 1,
            ["versionNonce"] = rnd.Next(1, int.MaxValue),
            ["isDeleted"] = false,
            ["boundElements"] = null,
            ["updated"] = now,
            ["link"] = null,
            ["locked"] = false,
        };

    private static Dictionary<string, object?> ExcalidrawShape(ShapeNode s, Random rnd, long now)
    {
        var el = ExcalidrawBase(s.Id, ExcalidrawType(s.Kind), s.X, s.Y, s.Width, s.Height, rnd, now);
        el["strokeColor"] = s.Stroke;
        el["backgroundColor"] = s.Fill;
        if (s.Kind is ShapeKind.Rounded or ShapeKind.ServiceTile or ShapeKind.Note)
            el["roundness"] = new Dictionary<string, object?> { ["type"] = 3 };
        return el;
    }

    private static Dictionary<string, object?> ExcalidrawLabel(ShapeNode s, Random rnd, long now)
    {
        const double fontSize = 16;
        double w = Math.Max(20, s.Width - 16);
        double h = fontSize * 1.25;
        var el = ExcalidrawBase(s.Id + "_t", "text", s.X + (s.Width - w) / 2, s.Y + (s.Height - h) / 2, w, h, rnd, now);
        el["strokeColor"] = "#1e1e1e";
        el["text"] = s.Label;
        el["originalText"] = s.Label;
        el["fontSize"] = fontSize;
        el["fontFamily"] = 2;          // 2 = normal / Helvetica-like
        el["textAlign"] = "center";
        el["verticalAlign"] = "middle";
        el["baseline"] = fontSize * 0.85;
        el["containerId"] = null;
        el["lineHeight"] = 1.25;
        return el;
    }

    private static Dictionary<string, object?> ExcalidrawArrow(ShapeNode from, ShapeNode to, Connection c, Random rnd, long now)
    {
        var (sx, sy) = EdgeIntersect(from, to.X + to.Width / 2, to.Y + to.Height / 2);
        var (ex, ey) = EdgeIntersect(to, from.X + from.Width / 2, from.Y + from.Height / 2);

        var minX = Math.Min(sx, ex);
        var minY = Math.Min(sy, ey);
        var el = ExcalidrawBase(c.Id, "arrow", minX, minY, Math.Abs(ex - sx), Math.Abs(ey - sy), rnd, now);
        el["strokeColor"] = c.Stroke;
        if (c.Dashed) el["strokeStyle"] = "dashed";
        el["points"] = new List<double[]> { new[] { sx - minX, sy - minY }, new[] { ex - minX, ey - minY } };
        el["lastCommittedPoint"] = null;
        el["startBinding"] = null;
        el["endBinding"] = null;
        el["startArrowhead"] = null;
        el["endArrowhead"] = "arrow";
        return el;
    }

    private static (double X, double Y) EdgeIntersect(ShapeNode n, double tx, double ty)
    {
        double cx = n.X + n.Width / 2, cy = n.Y + n.Height / 2;
        double dx = tx - cx, dy = ty - cy;
        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) return (cx, cy);
        double halfW = n.Width / 2, halfH = n.Height / 2;
        switch (n.Kind)
        {
            case ShapeKind.Ellipse:
            case ShapeKind.Person:
            {
                double t = 1.0 / Math.Sqrt((dx * dx) / (halfW * halfW) + (dy * dy) / (halfH * halfH));
                return (cx + dx * t, cy + dy * t);
            }
            case ShapeKind.Diamond:
            {
                double t = 1.0 / (Math.Abs(dx) / halfW + Math.Abs(dy) / halfH);
                return (cx + dx * t, cy + dy * t);
            }
            default:
            {
                double sx = dx != 0 ? halfW / Math.Abs(dx) : double.MaxValue;
                double sy = dy != 0 ? halfH / Math.Abs(dy) : double.MaxValue;
                double t = Math.Min(sx, sy);
                return (cx + dx * t, cy + dy * t);
            }
        }
    }
}

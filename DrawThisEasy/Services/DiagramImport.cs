using System;
using System.Collections.Generic;
using System.Text.Json;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services;

/// Imports an Excalidraw scene (.excalidraw JSON) into a DiagramModel.
/// Maps rectangle/ellipse/diamond/text elements to shapes, bound text to labels,
/// and arrows to connections (using start/end bindings, with a geometric fallback).
public static class DiagramImport
{
    public static DiagramModel FromExcalidraw(string json)
    {
        var model = new DiagramModel { Title = "Imported" };
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("elements", out var els) || els.ValueKind != JsonValueKind.Array)
            return model;

        var idMap = new Dictionary<string, ShapeNode>();     // excalidraw element id -> shape
        var boundLabels = new Dictionary<string, string>();   // containerId -> text
        var arrows = new List<ArrowRef>();
        int z = 1;

        // Image bytes live in a separate "files" map keyed by fileId.
        var files = new Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Object)
            foreach (var f in filesEl.EnumerateObject())
            {
                var url = GetStr(f.Value, "dataURL");
                if (!string.IsNullOrEmpty(url)) files[f.Name] = url!;
            }

        foreach (var e in els.EnumerateArray())
        {
            if (GetBool(e, "isDeleted")) continue;
            switch (GetStr(e, "type"))
            {
                case "rectangle":
                case "ellipse":
                case "diamond":
                {
                    var node = new ShapeNode
                    {
                        Kind = MapKind(GetStr(e, "type")!, e),
                        X = GetD(e, "x"), Y = GetD(e, "y"),
                        Width = Math.Max(10, GetD(e, "width")),
                        Height = Math.Max(10, GetD(e, "height")),
                        Fill = SanitizeColor(GetStr(e, "backgroundColor")) ?? "#FFFFFF",
                        Stroke = SanitizeColor(GetStr(e, "strokeColor")) ?? "#334155",
                        ZIndex = z++
                    };
                    var id = GetStr(e, "id");
                    if (id != null) idMap[id] = node;
                    model.Shapes.Add(node);
                    break;
                }
                case "text":
                {
                    var text = GetStr(e, "text") ?? "";
                    var container = GetStr(e, "containerId");
                    if (!string.IsNullOrEmpty(container))
                    {
                        boundLabels[container] = text;   // a shape's label — applied below
                    }
                    else
                    {
                        var node = new ShapeNode
                        {
                            Kind = ShapeKind.Text,
                            X = GetD(e, "x"), Y = GetD(e, "y"),
                            Width = Math.Max(20, GetD(e, "width")),
                            Height = Math.Max(16, GetD(e, "height")),
                            Label = text,
                            Fill = "#FFFFFF",
                            Stroke = SanitizeColor(GetStr(e, "strokeColor")) ?? "#334155",
                            ZIndex = z++
                        };
                        var id = GetStr(e, "id");
                        if (id != null) idMap[id] = node;
                        model.Shapes.Add(node);
                    }
                    break;
                }
                case "arrow":
                case "line":
                {
                    var (sx, sy, ex, ey) = ArrowEndpoints(e);
                    arrows.Add(new ArrowRef(
                        GetBindingId(e, "startBinding"), GetBindingId(e, "endBinding"),
                        sx, sy, ex, ey,
                        SanitizeColor(GetStr(e, "strokeColor")) ?? "#334155",
                        GetStr(e, "strokeStyle") == "dashed"));
                    break;
                }
                case "image":
                {
                    var fileId = GetStr(e, "fileId");
                    var node = new ShapeNode
                    {
                        Kind = ShapeKind.Image,
                        Image = fileId != null && files.TryGetValue(fileId, out var url) ? url : null,
                        X = GetD(e, "x"), Y = GetD(e, "y"),
                        Width = Math.Max(10, GetD(e, "width")),
                        Height = Math.Max(10, GetD(e, "height")),
                        Label = "",
                        ZIndex = z++
                    };
                    var id = GetStr(e, "id");
                    if (id != null) idMap[id] = node;
                    model.Shapes.Add(node);
                    break;
                }
            }
        }

        foreach (var (containerId, text) in boundLabels)
            if (idMap.TryGetValue(containerId, out var node))
                node.Label = text;

        foreach (var a in arrows)
        {
            var from = (a.From != null && idMap.TryGetValue(a.From, out var f)) ? f : HitShape(model, a.Sx, a.Sy);
            var to   = (a.To   != null && idMap.TryGetValue(a.To,   out var t)) ? t : HitShape(model, a.Ex, a.Ey);
            if (from != null && to != null && from != to)
                model.Connections.Add(new Connection { FromId = from.Id, ToId = to.Id, Stroke = a.Stroke, Dashed = a.Dashed });
        }

        return model;
    }

    private readonly record struct ArrowRef(string? From, string? To, double Sx, double Sy, double Ex, double Ey, string Stroke, bool Dashed);

    private static ShapeKind MapKind(string type, JsonElement e) => type switch
    {
        "ellipse" => ShapeKind.Ellipse,
        "diamond" => ShapeKind.Diamond,
        _ => e.TryGetProperty("roundness", out var r) && r.ValueKind == JsonValueKind.Object
                ? ShapeKind.Rounded : ShapeKind.Rectangle
    };

    private static (double Sx, double Sy, double Ex, double Ey) ArrowEndpoints(JsonElement e)
    {
        double x = GetD(e, "x"), y = GetD(e, "y");
        if (e.TryGetProperty("points", out var pts) && pts.ValueKind == JsonValueKind.Array && pts.GetArrayLength() >= 1)
        {
            JsonElement first = default, last = default;
            bool got = false;
            foreach (var p in pts.EnumerateArray()) { if (!got) { first = p; got = true; } last = p; }
            return (x + PointComp(first, 0), y + PointComp(first, 1), x + PointComp(last, 0), y + PointComp(last, 1));
        }
        return (x, y, x + GetD(e, "width"), y + GetD(e, "height"));
    }

    private static double PointComp(JsonElement pt, int i)
        => pt.ValueKind == JsonValueKind.Array && pt.GetArrayLength() > i && pt[i].ValueKind == JsonValueKind.Number
            ? pt[i].GetDouble() : 0;

    private static ShapeNode? HitShape(DiagramModel m, double x, double y)
    {
        const double pad = 2;
        for (int i = m.Shapes.Count - 1; i >= 0; i--)   // topmost first
        {
            var s = m.Shapes[i];
            if (x >= s.X - pad && x <= s.X + s.Width + pad && y >= s.Y - pad && y <= s.Y + s.Height + pad)
                return s;
        }
        return null;
    }

    private static string? SanitizeColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        if (c.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return null;
        if (c[0] != '#') return null;
        var hex = c.Length > 7 ? c.Substring(0, 7) : c;        // drop any alpha (#rrggbbaa -> #rrggbb)
        if (hex.Length == 4)                                    // #rgb -> #rrggbb
            hex = $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
        if (hex.Length != 7) return null;
        for (int i = 1; i < hex.Length; i++)
            if (!Uri.IsHexDigit(hex[i])) return null;
        return hex;
    }

    private static string? GetStr(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static double GetD(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;

    private static bool GetBool(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static string? GetBindingId(JsonElement e, string side)
        => e.TryGetProperty(side, out var b) && b.ValueKind == JsonValueKind.Object
           && b.TryGetProperty("elementId", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() : null;
}

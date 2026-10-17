using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services.Ai;

/// The engine-agnostic diagram protocol. The AI is taught our primitives (shape kinds,
/// palette, cloud stencils, connector styles) in a system prompt and answers with a small
/// JSON "diagram spec", which is validated and converted into a DiagramModel here. The
/// spec uses its own short ids so the AI never sees (or has to echo) our GUIDs.
public static class AiDiagram
{
    public record Result(DiagramModel Model, string? Summary);

    /// Asks the provider for a diagram. With `current`, the AI edits that diagram and the
    /// result is the complete updated diagram (unchanged shapes keep their images/RTF).
    public static async Task<Result> GenerateAsync(IAiProvider provider, string request, DiagramModel? current,
                                                   IProgress<int>? received, CancellationToken ct)
    {
        Dictionary<string, ShapeNode>? origById = null;
        var user = new StringBuilder();
        if (current != null && current.Shapes.Count > 0)
        {
            user.AppendLine("Here is the CURRENT diagram. Apply the request to it and return the COMPLETE updated diagram")
                .AppendLine("(keep the ids of shapes you keep; omit shapes that should be removed).")
                .AppendLine("<current_diagram>")
                .AppendLine(ToSpecJson(current, out origById))
                .AppendLine("</current_diagram>")
                .AppendLine();
        }
        user.AppendLine("<request>").AppendLine(request.Trim()).AppendLine("</request>");

        var text = await provider.CompleteAsync(SystemPrompt(), user.ToString(), received, ct);
        return Parse(text, origById);
    }

    // ---------- prompt ----------

    private static readonly (ShapeKind Kind, string Use)[] Kinds =
    {
        (ShapeKind.Rectangle,     "process step, generic box, container/zone/boundary (put containers first so they sit behind)"),
        (ShapeKind.Rounded,       "component, service, team, org-chart role"),
        (ShapeKind.Ellipse,       "start/end terminator, actor-less state, concept bubble"),
        (ShapeKind.Diamond,       "decision (label as a question; label outgoing edges e.g. Yes/No)"),
        (ShapeKind.Hexagon,       "load balancer, gateway, router, preparation step"),
        (ShapeKind.Parallelogram, "input/output, data in flight"),
        (ShapeKind.Cylinder,      "database, data store, disk"),
        (ShapeKind.Cloud,         "internet, external network, a cloud"),
        (ShapeKind.Server,        "server, VM, host, machine"),
        (ShapeKind.Person,        "user, actor, customer, role-player"),
        (ShapeKind.Queue,         "message queue, topic, stream, buffer"),
        (ShapeKind.Note,          "annotation, comment, sticky note"),
        (ShapeKind.Text,          "free text: titles, headings, captions (no border)"),
        (ShapeKind.ServiceTile,   "a specific cloud-provider service; requires \"stencil\""),
    };

    private static string? _systemPrompt;

    public static string SystemPrompt()
    {
        if (_systemPrompt != null) return _systemPrompt;
        var sb = new StringBuilder();
        sb.AppendLine("""
You are the diagram designer inside DrawThisEasy, a Windows app for org charts, architecture diagrams, flowcharts and system flows.
The user describes a diagram; you design it using ONLY the app's primitives below and answer with ONE JSON object — no prose, no markdown fences.

# Output format
{
  "title": "Short diagram title",
  "summary": "One sentence describing what you drew or changed.",
  "shapes": [
    { "id": "s1", "kind": "rounded", "label": "API", "x": 0, "y": 0, "w": 140, "h": 70,
      "fill": "#DBEAFE", "stroke": "#334155",
      "fontSize": 13, "bold": false, "fontColor": "#0F172A", "stencil": null }
  ],
  "connections": [
    { "from": "s1", "to": "s2", "label": "", "routing": "straight", "style": "solid", "stroke": "#334155" }
  ]
}
Only "id", "kind" and "label" are required on a shape; only "from" and "to" on a connection. Omit fields you don't need.

# Shape kinds (default size w×h)
""");
        foreach (var (kind, use) in Kinds)
        {
            var (w, h) = DefaultSize(kind);
            sb.AppendLine($"- {KindName(kind)} ({w}×{h}): {use}");
        }
        sb.AppendLine("""

# Colors — use ONLY these hex values (the app's palette; never purple)
Fills:   #FFFFFF white, #F1F5F9 slate, #DBEAFE blue, #CFFAFE cyan, #CCFBF1 teal, #D1FAE5 green, #FEF3C7 amber, #FFE4E6 rose, #FFEDD5 orange
Strokes: #334155 slate (default), #0F172A ink, #0EA5E9 sky, #14B8A6 teal, #10B981 emerald, #F59E0B amber, #EF4444 red, #475569 gray
Use color to encode meaning (e.g. one fill per tier or department), not decoration. Containers/zones: fill #F1F5F9 (or #FFFFFF) with stroke #475569 or #0EA5E9.

# Connections
routing: "straight" | "curved" | "elbow" (elbow suits org charts and tidy architecture). style: "solid" | "dashed" | "dotted" (dashed = async/optional).
Connectors attach to shape edges automatically — just name the endpoints. Label edges with protocols, actions or conditions when it helps.

# Cloud service tiles
For a named AWS/Azure/Google Cloud service use kind "servicetile" with one of these stencil ids (label defaults to the service name):
""");
        foreach (var g in Stencils.All.GroupBy(s => s.Provider))
            sb.AppendLine($"{g.Key}: " + string.Join(", ", g.Select(s => $"{s.Id} ({s.Name})")));
        sb.AppendLine("""
For services not in this list, use the closest generic kind (server, cylinder, queue, rounded...) with the service name as the label.

# Layout rules
- Coordinates are pixels, y grows downward, origin top-left; x/y are the shape's top-left corner. Snap positions to multiples of 10.
- Lay out deliberately: hierarchies top-down, flows left-to-right (or top-down for flowcharts), tiers in rows/columns.
- Leave clear gaps: ≥ 60px between neighbors, ≥ 100px between rows/tiers. Never overlap shapes, except shapes placed INSIDE a container.
- A container is a large rectangle listed BEFORE the shapes it holds, sized to enclose them with ≥ 30px padding, labeled with a text shape or its own label.
- Keep labels short (1–4 words; use \n for a second line). Widen a shape rather than cramming text.
- Prefer a clean, readable diagram over an exhaustive one; ~5–30 shapes is typical.
""");
        return _systemPrompt = sb.ToString();
    }

    private static string KindName(ShapeKind k) => k.ToString().ToLowerInvariant();

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

    // ---------- current diagram -> spec ----------

    private static string ToSpecJson(DiagramModel m, out Dictionary<string, ShapeNode> origById)
    {
        origById = new();
        var idFor = new Dictionary<string, string>();
        var shapes = new List<object>();
        int n = 1;
        foreach (var s in m.Shapes.OrderBy(s => s.ZIndex))
        {
            if (s.Kind is ShapeKind.Image) continue;   // images can't be described usefully
            var id = "s" + n++;
            idFor[s.Id] = id;
            origById[id] = s;
            shapes.Add(new Dictionary<string, object?>
            {
                ["id"] = id, ["kind"] = KindName(s.Kind == ShapeKind.RichText ? ShapeKind.Note : s.Kind),
                ["label"] = s.Label, ["x"] = Math.Round(s.X), ["y"] = Math.Round(s.Y),
                ["w"] = Math.Round(s.Width), ["h"] = Math.Round(s.Height),
                ["fill"] = s.Fill, ["stroke"] = s.Stroke, ["stencil"] = s.Stencil,
            });
        }
        var conns = m.Connections
            .Where(c => idFor.ContainsKey(c.FromId) && idFor.ContainsKey(c.ToId))
            .Select(c => new Dictionary<string, object?>
            {
                ["from"] = idFor[c.FromId], ["to"] = idFor[c.ToId], ["label"] = c.Label,
                ["routing"] = c.Routing.ToString().ToLowerInvariant(), ["style"] = c.StrokeStyle.ToString().ToLowerInvariant(),
                ["stroke"] = c.Stroke,
            }).ToList();
        return JsonSerializer.Serialize(new { title = m.Title, shapes, connections = conns });
    }

    // ---------- spec -> model ----------

    public static Result Parse(string text, Dictionary<string, ShapeNode>? origById = null)
    {
        var json = ExtractJson(text) ?? throw new AiException(L10n.T("ai.err.parse"));
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); }
        catch (JsonException) { throw new AiException(L10n.T("ai.err.parse")); }

        using (doc)
        {
            var root = doc.RootElement;
            var model = new DiagramModel { Title = Str(root, "title") ?? "AI diagram" };
            var byId = new Dictionary<string, ShapeNode>(StringComparer.OrdinalIgnoreCase);
            bool missingPos = false;
            int z = 0;

            if (Prop(root, "shapes") is { ValueKind: JsonValueKind.Array } shapes)
            {
                foreach (var e in shapes.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    var id = Str(e, "id") ?? Guid.NewGuid().ToString("N");
                    var kind = ParseKind(Str(e, "kind"));
                    var stencil = Stencils.Find(Str(e, "stencil"));
                    if (kind == ShapeKind.ServiceTile && stencil == null) kind = ShapeKind.Rounded;

                    ShapeNode? orig = null;
                    origById?.TryGetValue(id, out orig);
                    var (dw, dh) = DefaultSize(kind);
                    var node = new ShapeNode
                    {
                        Kind = kind,
                        Label = (Str(e, "label") ?? stencil?.Name ?? "").Replace("\\n", "\n"),
                        Width = Clamp(Num(e, "w") ?? Num(e, "width") ?? orig?.Width ?? dw, 20, 4000),
                        Height = Clamp(Num(e, "h") ?? Num(e, "height") ?? orig?.Height ?? dh, 16, 4000),
                        Fill = PaletteColor(Str(e, "fill"), Palette.Fills) ?? orig?.Fill ?? "#FFFFFF",
                        Stroke = stencil?.Color ?? PaletteColor(Str(e, "stroke"), Palette.Strokes) ?? orig?.Stroke ?? "#334155",
                        Stencil = stencil?.Id,
                        FontSize = Num(e, "fontSize") is { } fs ? Clamp(fs, 8, 72) : orig?.FontSize,
                        Bold = Bool(e, "bold") ?? orig?.Bold ?? false,
                        FontColor = AnyHex(Str(e, "fontColor")) ?? orig?.FontColor,
                        ZIndex = z++,
                    };
                    // Carry over what the spec can't express from the shape being edited.
                    if (orig != null && orig.Kind == ShapeKind.RichText && kind == ShapeKind.Note)
                    {
                        node.Kind = ShapeKind.RichText;
                        node.Rtf = orig.Label == node.Label ? orig.Rtf : null;
                    }
                    if (orig != null)
                    {
                        node.FontFamily = orig.FontFamily; node.Italic = orig.Italic; node.Underline = orig.Underline;
                        node.TextAlign = orig.TextAlign; node.Rotation = orig.Rotation; node.GroupId = orig.GroupId;
                    }
                    if (kind == ShapeKind.Text && Str(e, "fill") == null) node.Fill = "#FFFFFF";

                    if (Num(e, "x") is { } x && Num(e, "y") is { } y) { node.X = x; node.Y = y; }
                    else missingPos = true;

                    byId[id] = node;
                    model.Shapes.Add(node);
                }
            }
            if (model.Shapes.Count == 0) throw new AiException(L10n.T("ai.err.empty"));

            if (Prop(root, "connections") is { ValueKind: JsonValueKind.Array } conns)
            {
                foreach (var e in conns.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    if (!byId.TryGetValue(Str(e, "from") ?? "", out var a) || !byId.TryGetValue(Str(e, "to") ?? "", out var b) || a == b) continue;
                    model.Connections.Add(new Connection
                    {
                        FromId = a.Id, ToId = b.Id,
                        Label = (Str(e, "label") ?? "").Replace("\\n", "\n"),
                        Stroke = PaletteColor(Str(e, "stroke"), Palette.Strokes) ?? "#334155",
                        Routing = Str(e, "routing")?.ToLowerInvariant() switch
                        {
                            "curved" => ConnectorRouting.Curved, "elbow" => ConnectorRouting.Elbow,
                            _ => AppSettings.Current.DefaultRouting
                        },
                        StrokeStyle = Str(e, "style")?.ToLowerInvariant() switch
                        {
                            "dashed" => StrokeStyle.Dashed, "dotted" => StrokeStyle.Dotted, _ => StrokeStyle.Solid
                        },
                    });
                }
            }

            if (missingPos) AutoLayout(model);
            return new Result(model, Str(root, "summary"));
        }
    }

    /// Pulls the JSON object out of a reply that may carry fences or stray prose.
    private static string? ExtractJson(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    /// Fallback when the AI omitted coordinates: longest-path layering, top-down.
    private static void AutoLayout(DiagramModel m)
    {
        var level = m.Shapes.ToDictionary(s => s.Id, _ => 0);
        for (int pass = 0; pass < m.Shapes.Count; pass++)
        {
            bool changed = false;
            foreach (var c in m.Connections)
                if (level[c.ToId] < level[c.FromId] + 1 && level[c.FromId] + 1 < m.Shapes.Count)
                { level[c.ToId] = level[c.FromId] + 1; changed = true; }
            if (!changed) break;
        }
        double y = 0;
        foreach (var row in m.Shapes.GroupBy(s => level[s.Id]).OrderBy(g => g.Key))
        {
            double x = 0, rowH = 0;
            foreach (var s in row) { s.X = x; s.Y = y; x += s.Width + 60; rowH = Math.Max(rowH, s.Height); }
            y += rowH + 100;
        }
    }

    private static ShapeKind ParseKind(string? k)
    {
        var key = (k ?? "").Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        return key switch
        {
            "roundedrectangle" or "roundrect" => ShapeKind.Rounded,
            "circle" or "oval" => ShapeKind.Ellipse,
            "database" or "db" => ShapeKind.Cylinder,
            "user" or "actor" => ShapeKind.Person,
            "service" or "cloudservice" or "tile" => ShapeKind.ServiceTile,
            "label" or "title" => ShapeKind.Text,
            "image" or "richtext" => ShapeKind.Note,
            _ => Enum.TryParse<ShapeKind>(key, true, out var r) && r is not (ShapeKind.Image or ShapeKind.RichText) ? r : ShapeKind.Rectangle,
        };
    }

    private static string? PaletteColor(string? hex, string[] palette)
    {
        var h = AnyHex(hex);
        return h == null ? null : palette.FirstOrDefault(p => p.Equals(h, StringComparison.OrdinalIgnoreCase));
    }

    private static string? AnyHex(string? hex)
    {
        if (hex == null) return null;
        var h = hex.Trim();
        return h.Length == 7 && h[0] == '#' && h.Skip(1).All(Uri.IsHexDigit) ? h.ToUpperInvariant() : null;
    }

    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    private static JsonElement? Prop(JsonElement e, string name)
    {
        foreach (var p in e.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }

    private static string? Str(JsonElement e, string name) =>
        Prop(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static double? Num(JsonElement e, string name) => Prop(e, name) switch
    {
        { ValueKind: JsonValueKind.Number } v => v.GetDouble(),
        { ValueKind: JsonValueKind.String } v when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null
    };

    private static bool? Bool(JsonElement e, string name) => Prop(e, name) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null
    };
}

using System.IO;
using System.Text.Json;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services;

public static class Persistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Save(DiagramModel model, string path)
    {
        var json = JsonSerializer.Serialize(model, Options);
        File.WriteAllText(path, json);
    }

    public static DiagramModel Load(string path)
    {
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<DiagramModel>(json, Options);
        return model ?? new DiagramModel();
    }

    public static string ToJson(DiagramModel model) => JsonSerializer.Serialize(model, Options);
}

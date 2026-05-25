using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DrawThisEasy.Services;

/// Persists the most-recently opened/saved file paths (most recent first, capped at Max)
/// to %LOCALAPPDATA%\DrawThisEasy\recent.json.
public static class RecentFiles
{
    public const int Max = 5;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DrawThisEasy", "recent.json");

    public static List<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath));
                if (list != null) return list;
            }
        }
        catch { /* corrupt or unreadable — start fresh */ }
        return new List<string>();
    }

    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > Max) list = list.Take(Max).ToList();
        Save(list);
    }

    public static void Clear() => Save(new List<string>());

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch { /* best-effort */ }
    }
}

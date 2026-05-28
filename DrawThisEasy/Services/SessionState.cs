using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DrawThisEasy.Services;

/// Persists the set of file paths open at app shutdown so the next launch can reopen them.
/// Stored at %LOCALAPPDATA%\DrawThisEasy\session.json. Only paths of saved documents are recorded.
public static class SessionState
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DrawThisEasy", "session.json");

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

    public static void Save(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(paths));
        }
        catch { /* best-effort */ }
    }

    public static void Clear() => Save(Array.Empty<string>());
}

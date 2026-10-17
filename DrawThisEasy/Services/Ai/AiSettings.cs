using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrawThisEasy.Services.Ai;

/// AI engine preferences, persisted to %LOCALAPPDATA%\DrawThisEasy\ai-settings.json.
/// Kept apart from AppSettings so the Preferences dialog (which rebuilds AppSettings from
/// its own fields) can never drop them. The API key is stored DPAPI-encrypted for the
/// current Windows user — never in plain text.
public class AiSettings
{
    /// Id of the active provider (see AiProviders.All).
    public string Provider { get; set; } = AiProviders.ClaudeCodeId;

    /// Model per provider id; missing entries fall back to the provider's default.
    public System.Collections.Generic.Dictionary<string, string> Models { get; set; } = new();

    /// Optional explicit path to the Claude Code CLI; empty = auto-detect.
    public string ClaudeCliPath { get; set; } = "";

    /// DPAPI-protected, base64-encoded Anthropic API key. Empty = use ANTHROPIC_API_KEY.
    public string ApiKeyProtected { get; set; } = "";

    [JsonIgnore]
    public static AiSettings Current { get; private set; } = Load();

    [JsonIgnore]
    public string ApiKey
    {
        get
        {
            if (string.IsNullOrEmpty(ApiKeyProtected)) return "";
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return ""; }
        }
        set
        {
            ApiKeyProtected = string.IsNullOrWhiteSpace(value) ? "" :
                Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }
    }

    public string ModelFor(AiProviderInfo p) =>
        Models.TryGetValue(p.Id, out var m) && !string.IsNullOrWhiteSpace(m) ? m : p.DefaultModel;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DrawThisEasy", "ai-settings.json");

    public static AiSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { /* fall back to defaults */ }
        return new AiSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            Current = this;
        }
        catch { /* best-effort */ }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services;

public enum RulerUnit { Pixels, Centimeters, Inches, Picas }

/// User preferences, persisted to %LOCALAPPDATA%\DrawThisEasy\preferences.json.
public class AppSettings
{
    public ConnectorRouting DefaultRouting { get; set; } = ConnectorRouting.Straight;
    public StrokeStyle DefaultStroke { get; set; } = StrokeStyle.Solid;
    public bool SnapEnabled { get; set; } = true;
    public RulerUnit Units { get; set; } = RulerUnit.Pixels;
    public bool AutosaveEnabled { get; set; } = false;
    public int AutosaveIntervalSeconds { get; set; } = 60;
    public bool RestoreOpenFilesOnStartup { get; set; } = false;

    // Toolbar customization. ShowMainToolbar hides the built-in icon strip;
    // ShowFavoritesToolbar shows a second, user-curated row of buttons.
    // FavoriteToolbarItems holds short ids: ToolMode names ("AddRectangle"), or
    // the actions "Templates", "Image", "Cloud.AWS", "Cloud.Azure", "Cloud.Gcp".
    public bool ShowMainToolbar { get; set; } = true;
    public bool ShowFavoritesToolbar { get; set; } = false;
    public List<string> FavoriteToolbarItems { get; set; } = new();

    [JsonIgnore]
    public static AppSettings Current { get; private set; } = Load();

    /// Pixels (1 world unit = 1px @96dpi) per ruler unit.
    public static double UnitPixels(RulerUnit u) => u switch
    {
        RulerUnit.Centimeters => 96.0 / 2.54,
        RulerUnit.Inches      => 96.0,
        RulerUnit.Picas       => 16.0,
        _                     => 1.0,
    };

    public static string UnitSuffix(RulerUnit u) => u switch
    {
        RulerUnit.Centimeters => "cm",
        RulerUnit.Inches      => "in",
        RulerUnit.Picas       => "pc",
        _                     => "",
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DrawThisEasy", "preferences.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { /* fall back to defaults */ }
        return new AppSettings();
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

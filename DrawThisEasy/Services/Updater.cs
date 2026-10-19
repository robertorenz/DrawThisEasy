using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DrawThisEasy.Services;

/// A newer release found on GitHub, with the asset that fits this copy of the app.
public record UpdateInfo(Version Version, string Tag, string Notes, string PageUrl,
                         string AssetUrl, long AssetSize, string? AssetSha256, bool IsInstaller);

/// Checks GitHub Releases for a newer version, downloads it, and swaps it in.
/// Installed copies (Inno Setup's unins000.exe sits next to the exe) update by running the
/// new Setup silently; portable copies update by renaming the running exe aside and moving the
/// new one into its place (Windows allows renaming a running exe, just not overwriting it).
public static class Updater
{
    private const string LatestReleaseApi = "https://api.github.com/repos/robertorenz/DrawThisEasy/releases/latest";
    public const string ReleasesPage = "https://github.com/robertorenz/DrawThisEasy/releases";
    private const string SetupAssetName = "DrawThisEasy-Setup.exe";
    private const string PortableAssetName = "DrawThisEasy.exe";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"DrawThisEasy/{CurrentVersion}");   // GitHub rejects requests without one
        return c;
    }

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    private static string? ExePath => Environment.ProcessPath;

    /// True when this copy was installed by the Setup (vs. the portable exe).
    public static bool IsInstalled =>
        ExePath != null && File.Exists(Path.Combine(Path.GetDirectoryName(ExePath)!, "unins000.exe"));

    private static string OldExePath => ExePath + ".old";

    /// Deletes the previous exe left behind by a portable update. Call once at startup.
    public static void CleanupAfterUpdate()
    {
        try { if (ExePath != null && File.Exists(OldExePath)) File.Delete(OldExePath); }
        catch { /* the old process may still be exiting; try again next launch */ }
    }

    /// Returns the latest release if it is newer than this build, otherwise null.
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var resp = await Http.GetAsync(LatestReleaseApi, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
        latest = new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build));
        if (latest <= CurrentVersion) return null;

        bool installer = IsInstalled;
        var wanted = installer ? SetupAssetName : PortableAssetName;
        var asset = root.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), wanted, StringComparison.OrdinalIgnoreCase));
        if (asset.ValueKind != JsonValueKind.Object) return null;

        string? sha = null;
        if (asset.TryGetProperty("digest", out var d) && d.GetString() is string ds && ds.StartsWith("sha256:"))
            sha = ds["sha256:".Length..];

        return new UpdateInfo(
            latest, tag,
            root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
            root.TryGetProperty("html_url", out var h) ? h.GetString() ?? ReleasesPage : ReleasesPage,
            asset.GetProperty("browser_download_url").GetString()!,
            asset.GetProperty("size").GetInt64(),
            sha, installer);
    }

    /// Downloads the update to %TEMP% and verifies its size (and SHA-256 when GitHub publishes one).
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), "DrawThisEasy-update");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, info.IsInstaller ? SetupAssetName : PortableAssetName);

        using (var resp = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? info.AssetSize;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(dest);
            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress.Report((double)read / total);
            }
        }

        if (new FileInfo(dest).Length != info.AssetSize)
            throw new InvalidDataException("Downloaded file size does not match the release.");
        if (info.AssetSha256 != null)
        {
            await using var fs = File.OpenRead(dest);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            if (!hash.Equals(info.AssetSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Downloaded file failed its checksum.");
        }
        return dest;
    }

    /// Portable only: moves the downloaded exe into place, keeping the running one as *.old.
    /// Done before the app closes so a failure (e.g. read-only folder) can still be reported.
    public static void StagePortable(string downloaded)
    {
        var exe = ExePath ?? throw new InvalidOperationException("Unknown executable path.");
        if (File.Exists(OldExePath)) File.Delete(OldExePath);
        File.Move(exe, OldExePath);
        try { File.Move(downloaded, exe); }
        catch { File.Move(OldExePath, exe); throw; }
    }

    /// Launches the update; the caller exits right after. Installer: runs Setup silently (it
    /// relaunches the app when done). Portable: starts the already-staged new exe.
    public static void Launch(UpdateInfo info, string downloaded)
    {
        if (info.IsInstaller)
            Process.Start(new ProcessStartInfo(downloaded, "/SILENT /SP- /NOCANCEL /CLOSEAPPLICATIONS")
                { UseShellExecute = true });   // ShellExecute so Setup's UAC prompt can elevate
        else
            Process.Start(new ProcessStartInfo(ExePath!) { UseShellExecute = true });
    }
}

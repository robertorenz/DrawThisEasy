using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DrawThisEasy.Services.Ai;

/// Claude through the user's Claude subscription, by driving the signed-in Claude Code CLI
/// headlessly (`claude -p`). Runs with every tool disabled, customizations off, and in a
/// private scratch folder, so it can only answer — it can't touch the user's files.
public class ClaudeCodeProvider(string cliPath, string model) : IAiProvider
{
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, IProgress<int>? received, CancellationToken ct)
    {
        var exe = ResolveCli(cliPath) ?? throw new AiException(L10n.T("ai.err.nocli"));

        var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrawThisEasy", "ai-work");
        Directory.CreateDirectory(work);
        // Passed as a file: a multi-line prompt doesn't survive cmd.exe quoting when the
        // CLI is an npm .cmd shim.
        var sysFile = Path.Combine(work, $"system-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(sysFile, systemPrompt, ct);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = work,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[]
        {
            "-p", "--safe-mode", "--tools", "", "--strict-mcp-config", "--no-session-persistence",
            "--output-format", "stream-json", "--verbose", "--include-partial-messages",
            "--model", model, "--system-prompt-file", sysFile,
        }) psi.ArgumentList.Add(a);
        // An API key in the environment would take precedence over the subscription login.
        psi.Environment.Remove("ANTHROPIC_API_KEY");
        psi.Environment.Remove("ANTHROPIC_AUTH_TOKEN");

        using var proc = new Process { StartInfo = psi };
        try
        {
            if (!proc.Start()) throw new AiException(L10n.T("ai.err.nocli"));
        }
        catch (System.ComponentModel.Win32Exception) { throw new AiException(L10n.T("ai.err.nocli")); }

        using var reg = ct.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });
        try
        {
            var errTask = proc.StandardError.ReadToEndAsync();
            var stdin = new StreamWriter(proc.StandardInput.BaseStream, new UTF8Encoding(false));
            await stdin.WriteAsync(userPrompt);
            stdin.Close();

            var streamed = new StringBuilder();
            string? result = null;
            bool isError = false;

            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
            {
                if (line.Length == 0 || line[0] != '{') continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "stream_event"
                        && root.TryGetProperty("event", out var ev)
                        && ev.TryGetProperty("delta", out var d)
                        && d.TryGetProperty("text", out var txt))
                    {
                        streamed.Append(txt.GetString());
                        received?.Report(streamed.Length);
                    }
                    else if (root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String)
                    {
                        result = r.GetString();
                        isError = root.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True;
                    }
                }
                catch (JsonException) { /* not an event line */ }
            }

            await proc.WaitForExitAsync(ct);
            ct.ThrowIfCancellationRequested();
            var stderr = (await errTask).Trim();

            if (isError || (result == null && proc.ExitCode != 0))
            {
                var msg = result ?? stderr;
                if (msg.Contains("login", StringComparison.OrdinalIgnoreCase) || msg.Contains("auth", StringComparison.OrdinalIgnoreCase))
                    throw new AiException(L10n.T("ai.err.cli.login"));
                throw new AiException(L10n.T("ai.err.cli") + " " + Shorten(msg));
            }
            return result ?? streamed.ToString();
        }
        finally
        {
            try { File.Delete(sysFile); } catch { }
        }
    }

    private static string Shorten(string s) => s.Length > 400 ? s[..400] + "…" : s;

    /// Finds the Claude Code CLI: explicit path, then PATH, then the usual install locations.
    public static string? ResolveCli(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;

        var names = new[] { "claude.exe", "claude.cmd" };
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dirs.Add(Path.Combine(home, ".local", "bin"));
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude"));

        foreach (var dir in dirs)
            foreach (var n in names)
            {
                try
                {
                    var p = Path.Combine(dir.Trim('"'), n);
                    if (File.Exists(p)) return p;
                }
                catch { /* malformed PATH entry */ }
            }
        return null;
    }
}

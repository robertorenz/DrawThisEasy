using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;

namespace DrawThisEasy.Services.Ai;

/// Claude via the Anthropic API (official SDK). Key comes from the AI settings, else the
/// ANTHROPIC_API_KEY environment variable.
public class ClaudeApiProvider(string apiKey, string model) : IAiProvider
{
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, IProgress<int>? received, CancellationToken ct)
    {
        var key = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new AiException(L10n.T("ai.err.nokey"));

        var client = new AnthropicClient { ApiKey = key };
        var p = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 64000,
            System = systemPrompt,
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            Messages = [new() { Role = Role.User, Content = userPrompt }],
        };
        // Server-side refusal fallback: a policy decline is re-served by another model in
        // the same call instead of just stopping.
        if (model != "claude-opus-4-8")
        {
            p = p with
            {
                Betas = [AnthropicBeta.ServerSideFallback2026_06_01],
                Fallbacks = new List<BetaFallbackParam> { new() { Model = "claude-opus-4-8" } },
            };
        }

        var text = new StringBuilder();
        string? stopReason = null;
        try
        {
            // Streaming keeps a long design request clear of HTTP timeouts and lets the
            // dialog show progress as the diagram arrives.
            await foreach (var ev in client.Beta.Messages.CreateStreaming(p, ct))
            {
                if (ev.TryPickContentBlockDelta(out var d) && d.Delta.TryPickText(out var t))
                {
                    text.Append(t.Text);
                    received?.Report(text.Length);
                }
                else if (ev.TryPickDelta(out var md) && md.Delta.StopReason is { } sr)
                {
                    stopReason = sr.ToString();
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (AnthropicUnauthorizedException) { throw new AiException(L10n.T("ai.err.badkey")); }
        catch (AnthropicRateLimitException) { throw new AiException(L10n.T("ai.err.ratelimit")); }
        catch (AnthropicApiException ex) { throw new AiException(L10n.T("ai.err.api") + " " + ex.Message); }
        catch (AnthropicIOException ex) { throw new AiException(L10n.T("ai.err.network") + " " + ex.Message); }

        if (stopReason != null && stopReason.Contains("refusal", StringComparison.OrdinalIgnoreCase))
            throw new AiException(L10n.T("ai.err.refusal"));
        return text.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DrawThisEasy.Services.Ai;

/// One AI engine. Providers are deliberately text-in / text-out: the diagram protocol
/// (system prompt + JSON spec, see AiDiagram) is engine-agnostic, so adding another
/// engine (OpenAI, Ollama, ...) means implementing just this interface and registering
/// it in AiProviders.All.
public interface IAiProvider
{
    /// Returns the model's reply text. `received` (optional) reports characters received so
    /// far, for engines that stream.
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, IProgress<int>? received, CancellationToken ct);
}

/// A failure worth showing to the user as-is (already localized).
public class AiException(string message) : Exception(message);

/// Registry entry: id (persisted), localized name key, model choices, and a factory.
public record AiProviderInfo(
    string Id, string NameKey, string DescKey,
    IReadOnlyList<string> ModelChoices, string DefaultModel,
    Func<AiSettings, IAiProvider> Create)
{
    public string Name => L10n.T(NameKey);
    public string Description => L10n.T(DescKey);
}

public static class AiProviders
{
    public const string ClaudeCodeId = "claude-code";
    public const string ClaudeApiId  = "claude-api";

    public static readonly IReadOnlyList<AiProviderInfo> All = new List<AiProviderInfo>
    {
        // Uses the signed-in Claude Code CLI, so usage counts against the user's
        // Claude subscription. Aliases always resolve to the newest model in each tier.
        new(ClaudeCodeId, "ai.provider.code", "ai.provider.code.desc",
            new[] { "opus", "sonnet", "haiku" }, "opus",
            s => new ClaudeCodeProvider(s.ClaudeCliPath, s.ModelFor(Find(ClaudeCodeId)))),

        // Direct Anthropic API with an API key (pay-as-you-go).
        new(ClaudeApiId, "ai.provider.api", "ai.provider.api.desc",
            new[] { "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5", "claude-fable-5-1" }, "claude-opus-5-5",
            s => new ClaudeApiProvider(s.ApiKey, s.ModelFor(Find(ClaudeApiId)))),
    };

    public static AiProviderInfo Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

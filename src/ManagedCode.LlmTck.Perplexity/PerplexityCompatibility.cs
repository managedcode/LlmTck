using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Perplexity;

public static class PerplexityCompatibility
{
    public const string ProviderId = "perplexity";

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Perplexity",
            Protocol = LlmTckProtocolFamily.Perplexity,
            DefaultEndpointPath = "/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
            ],
            CompatibilityTags = ["perplexity", "openai-compatible"],
        };
}

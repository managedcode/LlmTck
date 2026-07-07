using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.OpenRouter;

public static class OpenRouterCompatibility
{
    public const string ProviderId = "openrouter";

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "OpenRouter",
            Protocol = LlmTckProtocolFamily.OpenRouter,
            DefaultEndpointPath = "/api/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = ["openrouter", "openai-compatible"],
        };
}

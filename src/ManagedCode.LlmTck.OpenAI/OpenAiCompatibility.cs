using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.OpenAI;

public static class OpenAiCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.OpenAI;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "OpenAI",
            Protocol = LlmTckProtocolFamily.OpenAI,
            DefaultEndpointPath = "/v1",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
        };
}

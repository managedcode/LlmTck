using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Cohere;

public static class CohereCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Cohere;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Cohere",
            Protocol = LlmTckProtocolFamily.Cohere,
            DefaultEndpointPath = "/v2/chat",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId],
        };
}

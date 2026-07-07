using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Mistral;

public static class MistralCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Mistral;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Mistral",
            Protocol = LlmTckProtocolFamily.Mistral,
            DefaultEndpointPath = "/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
        };
}

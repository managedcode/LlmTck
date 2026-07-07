using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Gemini;

public static class GeminiCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Gemini;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Gemini",
            Protocol = LlmTckProtocolFamily.Gemini,
            DefaultEndpointPath = "/v1beta/models/{model}:generateContent",
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
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.GoogleAI,
                LlmTckCompatibilityTags.OpenAICompatible,
            ],
        };
}

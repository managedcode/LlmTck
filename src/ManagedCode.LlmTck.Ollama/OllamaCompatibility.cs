using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Ollama;

public static class OllamaCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Ollama;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Ollama",
            Protocol = LlmTckProtocolFamily.Ollama,
            DefaultEndpointPath = "/api/chat",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Tools,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.LocalLlm,
                LlmTckCompatibilityTags.OpenAICompatible,
            ],
        };
}

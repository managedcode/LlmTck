using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Ollama;

public static class OllamaCompatibility
{
    public const string ProviderId = "ollama";

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
            CompatibilityTags = ["ollama", "local-llm", "openai-compatible"],
        };
}

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
            ApiContract = new()
            {
                DocumentationUrl = "https://docs.ollama.com/api/chat",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "local Ollama HTTP API",
                Operations =
                [
                    new()
                    {
                        Id = "chat.create",
                        Method = "POST",
                        Path = "/api/chat",
                        DocumentationUrl = "https://docs.ollama.com/api/chat",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                            LlmTckProviderCapability.Tools,
                        ],
                    },
                    new()
                    {
                        Id = "embeddings.create",
                        Method = "POST",
                        Path = "/api/embed",
                        DocumentationUrl = "https://docs.ollama.com/api/embed",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

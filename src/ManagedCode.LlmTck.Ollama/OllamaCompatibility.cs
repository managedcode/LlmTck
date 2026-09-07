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
            DefaultEndpointPath = "/ollama/api/chat",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
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
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "local Ollama HTTP API",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Ollama.ChatCreate,
                        Method = "POST",
                        Path = "/ollama/api/chat",
                        DocumentationUrl = "https://docs.ollama.com/api/chat",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Ollama.EmbeddingsCreate,
                        Method = "POST",
                        Path = "/ollama/api/embed",
                        DocumentationUrl = "https://docs.ollama.com/api/embed",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

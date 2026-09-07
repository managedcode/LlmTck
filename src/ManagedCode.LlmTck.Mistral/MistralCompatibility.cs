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
            DefaultEndpointPath = "/mistral/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://docs.mistral.ai/api/endpoint/chat",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "v1",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Mistral.ChatComplete,
                        Method = "POST",
                        Path = "/mistral/v1/chat/completions",
                        DocumentationUrl = "https://docs.mistral.ai/api/endpoint/chat",
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
                        Id = LlmTckProviderOperationIds.Mistral.EmbeddingsCreate,
                        Method = "POST",
                        Path = "/mistral/v1/embeddings",
                        DocumentationUrl = "https://docs.mistral.ai/api/endpoint/embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

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
            DefaultEndpointPath = "/cohere/v2/chat",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId],
            ApiContract = new()
            {
                DocumentationUrl = "https://docs.cohere.com/reference/chat",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "v2",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Cohere.ChatCreate,
                        Method = "POST",
                        Path = "/cohere/v2/chat",
                        DocumentationUrl = "https://docs.cohere.com/reference/chat",
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
                        Id = LlmTckProviderOperationIds.Cohere.EmbedCreate,
                        Method = "POST",
                        Path = "/cohere/v2/embed",
                        DocumentationUrl = "https://docs.cohere.com/reference/embed",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Perplexity;

public static class PerplexityCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Perplexity;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Perplexity",
            Protocol = LlmTckProtocolFamily.Perplexity,
            DefaultEndpointPath = "/perplexity/v1/sonar",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://docs.perplexity.ai/api-reference/sonar-post",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "Sonar API v1",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Perplexity.SonarCreate,
                        Method = "POST",
                        Path = "/perplexity/v1/sonar",
                        DocumentationUrl = "https://docs.perplexity.ai/api-reference/sonar-post",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                ],
            },
        };
}

using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.OpenRouter;

public static class OpenRouterCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.OpenRouter;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "OpenRouter",
            Protocol = LlmTckProtocolFamily.OpenRouter,
            DefaultEndpointPath = "/api/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Models,
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://openrouter.ai/docs/api/reference/overview",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "OpenAI-compatible /api/v1",
                Operations =
                [
                    new()
                    {
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/api/v1/chat/completions",
                        DocumentationUrl = "https://openrouter.ai/docs/api/reference/overview",
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
                        Id = "responses.create",
                        Method = "POST",
                        Path = "/api/v1/responses",
                        DocumentationUrl =
                            "https://openrouter.ai/docs/api/reference/responses/overview",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.StreamingChat,
                        ],
                    },
                    new()
                    {
                        Id = "models.list",
                        Method = "GET",
                        Path = "/api/v1/models",
                        DocumentationUrl =
                            "https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                ],
            },
        };
}

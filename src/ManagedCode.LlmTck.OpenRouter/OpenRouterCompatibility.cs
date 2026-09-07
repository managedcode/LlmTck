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
            DefaultEndpointPath = "/openrouter/api/v1/chat/completions",
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
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "OpenAI-compatible /api/v1",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.OpenRouter.ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/openrouter/api/v1/chat/completions",
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
                        Id = LlmTckProviderOperationIds.OpenRouter.ResponsesCreate,
                        Method = "POST",
                        Path = "/openrouter/api/v1/responses",
                        DocumentationUrl =
                            "https://openrouter.ai/docs/api/reference/responses/overview",
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
                        Id = LlmTckProviderOperationIds.OpenRouter.ModelsList,
                        Method = "GET",
                        Path = "/openrouter/api/v1/models",
                        DocumentationUrl =
                            "https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                ],
            },
        };
}

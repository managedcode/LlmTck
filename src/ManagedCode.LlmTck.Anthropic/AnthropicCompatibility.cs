using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Anthropic;

public static class AnthropicCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Anthropic;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Anthropic",
            Protocol = LlmTckProtocolFamily.AnthropicMessages,
            DefaultEndpointPath = "/anthropic/v1/messages",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.Claude,
                LlmTckCompatibilityTags.MessagesApi,
            ],
            ApiContract = new()
            {
                DocumentationUrl = "https://platform.claude.com/docs/en/api/messages/create",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "anthropic-version: 2023-06-01",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Anthropic.MessagesCreate,
                        Method = "POST",
                        Path = "/anthropic/v1/messages",
                        DocumentationUrl = "https://platform.claude.com/docs/en/api/messages/create",
                        RequiredHeader = "anthropic-version",
                        ApiVersion = "2023-06-01",
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
                ],
            },
        };
}

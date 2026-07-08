using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.DeepSeek;

public static class DeepSeekCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.DeepSeek;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "DeepSeek",
            Protocol = LlmTckProtocolFamily.DeepSeek,
            DefaultEndpointPath = "/deepseek/v1/chat/completions",
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
                DocumentationUrl = "https://api-docs.deepseek.com/",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "OpenAI-compatible v1",
                Operations =
                [
                    new()
                    {
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/deepseek/v1/chat/completions",
                        DocumentationUrl =
                            "https://api-docs.deepseek.com/api/create-chat-completion",
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
                        Id = "models.list",
                        Method = "GET",
                        Path = "/deepseek/models",
                        DocumentationUrl = "https://api-docs.deepseek.com/api/list-models",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                ],
            },
        };
}

using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Groq;

public static class GroqCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Groq;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Groq",
            Protocol = LlmTckProtocolFamily.Groq,
            DefaultEndpointPath = "/groq/openai/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Models,
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Audio,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://console.groq.com/docs/api-reference",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "OpenAI-compatible v1",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Groq.ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/groq/openai/v1/chat/completions",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
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
                        Id = LlmTckProviderOperationIds.Groq.ResponsesCreate,
                        Method = "POST",
                        Path = "/groq/openai/v1/responses",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
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
                        Id = LlmTckProviderOperationIds.Groq.AudioSpeechCreate,
                        Method = "POST",
                        Path = "/groq/openai/v1/audio/speech",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Groq.AudioTranscriptionsCreate,
                        Method = "POST",
                        Path = "/groq/openai/v1/audio/transcriptions",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Groq.AudioTranslationsCreate,
                        Method = "POST",
                        Path = "/groq/openai/v1/audio/translations",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Groq.ModelsList,
                        Method = "GET",
                        Path = "/groq/openai/v1/models",
                        DocumentationUrl = "https://console.groq.com/docs/models",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                ],
            },
        };
}

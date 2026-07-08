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
            DefaultEndpointPath = "/openai/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Models,
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
            ApiContract = new()
            {
                DocumentationUrl = "https://console.groq.com/docs/api-reference",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "OpenAI-compatible v1",
                Operations =
                [
                    new()
                    {
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/openai/v1/chat/completions",
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
                        Id = "responses.create",
                        Method = "POST",
                        Path = "/openai/v1/responses",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
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
                        Id = "audio.speech.create",
                        Method = "POST",
                        Path = "/openai/v1/audio/speech",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "audio.transcriptions.create",
                        Method = "POST",
                        Path = "/openai/v1/audio/transcriptions",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "audio.translations.create",
                        Method = "POST",
                        Path = "/openai/v1/audio/translations",
                        DocumentationUrl = "https://console.groq.com/docs/api-reference",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "models.list",
                        Method = "GET",
                        Path = "/openai/v1/models",
                        DocumentationUrl = "https://console.groq.com/docs/models",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Models],
                    },
                ],
            },
        };
}

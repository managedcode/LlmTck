using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Gemini;

public static class GeminiCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Gemini;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Gemini",
            Protocol = LlmTckProtocolFamily.Gemini,
            DefaultEndpointPath = "/v1beta/models/{model}:generateContent",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.Video,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.GoogleAI,
                LlmTckCompatibilityTags.OpenAICompatible,
            ],
            ApiContract = new()
            {
                DocumentationUrl = "https://ai.google.dev/api",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "v1beta Generative Language API",
                Operations =
                [
                    new()
                    {
                        Id = "models.generateContent",
                        Method = "POST",
                        Path = "/v1beta/models/{model}:generateContent",
                        DocumentationUrl = "https://ai.google.dev/api/generate-content",
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.Images,
                            LlmTckProviderCapability.Audio,
                            LlmTckProviderCapability.Tools,
                            LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                    new()
                    {
                        Id = "models.streamGenerateContent",
                        Method = "POST",
                        Path = "/v1beta/models/{model}:streamGenerateContent",
                        DocumentationUrl = "https://ai.google.dev/api/generate-content",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.StreamingChat],
                    },
                    new()
                    {
                        Id = "models.embedContent",
                        Method = "POST",
                        Path = "/v1beta/models/{model}:embedContent",
                        DocumentationUrl = "https://ai.google.dev/api/embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = "models.predictLongRunning.video",
                        Method = "POST",
                        Path = "/v1beta/models/{model}:predictLongRunning",
                        DocumentationUrl = "https://ai.google.dev/gemini-api/docs/video",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "models.operations.get.video",
                        Method = "GET",
                        Path = "/v1beta/models/{model}/operations/{operationId}",
                        DocumentationUrl = "https://ai.google.dev/gemini-api/docs/video",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "files.get.generatedVideo",
                        Method = "GET",
                        Path = "/v1beta/files/{fileId}",
                        DocumentationUrl = "https://ai.google.dev/api/files",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                ],
            },
        };
}

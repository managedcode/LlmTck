using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.AzureOpenAI;

public static class AzureOpenAiCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.AzureOpenAI;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Azure OpenAI",
            Protocol = LlmTckProtocolFamily.AzureOpenAI,
            DefaultEndpointPath = "/openai/deployments/{deployment}/chat/completions",
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
                LlmTckCompatibilityTags.OpenAICompatible,
                LlmTckCompatibilityTags.Foundry,
            ],
            ApiContract = new()
            {
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "2024-10-21 GA; v1 preview for video",
                Operations =
                [
                    new()
                    {
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/azureopenai/chat/create-chat-completion",
                        ApiVersion = "2024-10-21",
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
                        Id = "embeddings.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/azureopenai/embeddings/create",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = "images.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/images/generations",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Images],
                    },
                    new()
                    {
                        Id = "audio.speech.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/audio/speech",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "audio.transcriptions.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/audio/transcriptions",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "audio.translations.create",
                        Method = "POST",
                        Path = "/openai/deployments/{deployment}/audio/translations",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = "video.generation.jobs.create",
                        Method = "POST",
                        Path = "/openai/v1/video/generations/jobs",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generation.jobs.list",
                        Method = "GET",
                        Path = "/openai/v1/video/generations/jobs",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generation.jobs.retrieve",
                        Method = "GET",
                        Path = "/openai/v1/video/generations/jobs/{jobId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generation.jobs.delete",
                        Method = "DELETE",
                        Path = "/openai/v1/video/generations/jobs/{jobId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generations.retrieve",
                        Method = "GET",
                        Path = "/openai/v1/video/generations/{generationId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generations.thumbnail.retrieve",
                        Method = "GET",
                        Path = "/openai/v1/video/generations/{generationId}/content/thumbnail",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generations.content.retrieve",
                        Method = "GET",
                        Path = "/openai/v1/video/generations/{generationId}/content/video",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = "video.generations.content.head",
                        Method = "HEAD",
                        Path = "/openai/v1/video/generations/{generationId}/content/video",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                ],
            },
        };
}

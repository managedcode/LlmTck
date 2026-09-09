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
            DefaultEndpointPath = "/azure-openai/openai/deployments/{deployment}/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.Video,
                LlmTckProviderCapability.Audio,
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
                DocumentationRetrievedOn = "2026-09-09",
                DocumentationVersion = "SDK-selected dated deployment API (2024-10-21 reference baseline); v1 GA inference; v1 preview video",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.V1ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/v1/chat/completions",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Chat, LlmTckProviderCapability.StreamingChat, LlmTckProviderCapability.Tools, LlmTckProviderCapability.StructuredOutput],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.V1ResponsesCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/v1/responses",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Chat, LlmTckProviderCapability.StreamingChat, LlmTckProviderCapability.Tools, LlmTckProviderCapability.StructuredOutput],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.V1EmbeddingsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/v1/embeddings",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = false,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
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
                        Id = LlmTckProviderOperationIds.AzureOpenAI.EmbeddingsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.ImagesCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/images/generations",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Images],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.AudioSpeechCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/audio/speech",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.AudioTranscriptionsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/audio/transcriptions",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.AudioTranslationsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/deployments/{deployment}/audio/translations",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/reference",
                        ApiVersion = "2024-10-21",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Audio],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsCreate,
                        Method = "POST",
                        Path = "/azure-openai/openai/v1/video/generations/jobs",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsList,
                        Method = "GET",
                        Path = "/azure-openai/openai/v1/video/generations/jobs",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsRetrieve,
                        Method = "GET",
                        Path = "/azure-openai/openai/v1/video/generations/jobs/{jobId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsDelete,
                        Method = "DELETE",
                        Path = "/azure-openai/openai/v1/video/generations/jobs/{jobId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsRetrieve,
                        Method = "GET",
                        Path = "/azure-openai/openai/v1/video/generations/{generationId}",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsThumbnailRetrieve,
                        Method = "GET",
                        Path = "/azure-openai/openai/v1/video/generations/{generationId}/content/thumbnail",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentRetrieve,
                        Method = "GET",
                        Path = "/azure-openai/openai/v1/video/generations/{generationId}/content/video",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest",
                        ApiVersion = "v1 preview",
                        RequiredHeader = "api-key",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Video],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentHead,
                        Method = "HEAD",
                        Path = "/azure-openai/openai/v1/video/generations/{generationId}/content/video",
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

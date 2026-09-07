using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Foundry;

public static class MicrosoftFoundryCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.MicrosoftFoundry;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Microsoft Foundry",
            Protocol = LlmTckProtocolFamily.MicrosoftFoundry,
            DefaultEndpointPath = "/microsoft-foundry/models/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.AzureAIFoundry,
                LlmTckCompatibilityTags.OpenAICompatible,
            ],
            ApiContract = new()
            {
                DocumentationUrl =
                    "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/",
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "Azure AI Model Inference; OpenAI v1 GA",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.V1ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/openai/v1/chat/completions",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Chat, LlmTckProviderCapability.StreamingChat, LlmTckProviderCapability.Tools, LlmTckProviderCapability.StructuredOutput],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.V1ResponsesCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/openai/v1/responses",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Chat, LlmTckProviderCapability.StreamingChat, LlmTckProviderCapability.Tools, LlmTckProviderCapability.StructuredOutput],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.V1EmbeddingsCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/openai/v1/embeddings",
                        DocumentationUrl = "https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle",
                        ApiVersion = "v1",
                        SupportsStreaming = false,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ChatCompletionsCreate,
                        ApiVersion = "2024-05-01-preview",
                        Method = "POST",
                        Path = "/microsoft-foundry/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/",
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
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.EmbeddingsCreate,
                        ApiVersion = "2024-05-01-preview",
                        Method = "POST",
                        Path = "/microsoft-foundry/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ModelsChatCompletionsCreate,
                        ApiVersion = "2024-05-01-preview",
                        Method = "POST",
                        Path = "/microsoft-foundry/models/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/",
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
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ModelsEmbeddingsCreate,
                        ApiVersion = "2024-05-01-preview",
                        Method = "POST",
                        Path = "/microsoft-foundry/models/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

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
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
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
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "Azure AI Model Inference",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ChatCompletionsCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/chat-completions",
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
                        Method = "POST",
                        Path = "/microsoft-foundry/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/text-embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ModelsChatCompletionsCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/models/chat/completions",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/chat-completions",
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
                        Id = LlmTckProviderOperationIds.MicrosoftFoundry.ModelsEmbeddingsCreate,
                        Method = "POST",
                        Path = "/microsoft-foundry/models/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/text-embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

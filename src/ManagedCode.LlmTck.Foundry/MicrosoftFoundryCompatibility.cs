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
            DefaultEndpointPath = "/models/chat/completions",
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
                        Id = "chat.completions.create",
                        Method = "POST",
                        Path = "/chat/completions",
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
                        Id = "embeddings.create",
                        Method = "POST",
                        Path = "/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/text-embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                    new()
                    {
                        Id = "models.chat.completions.create",
                        Method = "POST",
                        Path = "/models/chat/completions",
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
                        Id = "models.embeddings.create",
                        Method = "POST",
                        Path = "/models/embeddings",
                        DocumentationUrl =
                            "https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/text-embeddings",
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.Embeddings],
                    },
                ],
            },
        };
}

using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Bedrock;

public static class BedrockCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Bedrock;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Amazon Bedrock",
            Protocol = LlmTckProtocolFamily.Bedrock,
            DefaultEndpointPath = "/model/{modelId}/converse",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags =
            [
                LlmTckCompatibilityTags.AmazonBedrock,
                LlmTckCompatibilityTags.AwsBedrock,
                ProviderId,
            ],
            ApiContract = new()
            {
                DocumentationUrl =
                    "https://docs.aws.amazon.com/bedrock/latest/APIReference/welcome.html",
                DocumentationRetrievedOn = "2026-07-08",
                DocumentationVersion = "bedrock-runtime",
                Operations =
                [
                    new()
                    {
                        Id = "converse",
                        Method = "POST",
                        Path = "/model/{modelId}/converse",
                        DocumentationUrl =
                            "https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_Converse.html",
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Chat,
                            LlmTckProviderCapability.Tools,
                            LlmTckProviderCapability.StructuredOutput,
                        ],
                    },
                    new()
                    {
                        Id = "converseStream",
                        Method = "POST",
                        Path = "/model/{modelId}/converse-stream",
                        DocumentationUrl =
                            "https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_ConverseStream.html",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.StreamingChat],
                    },
                    new()
                    {
                        Id = "invokeModel",
                        Method = "POST",
                        Path = "/model/{modelId}/invoke",
                        DocumentationUrl =
                            "https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_InvokeModel.html",
                        ImplementedByHosting = true,
                        Capabilities =
                        [
                            LlmTckProviderCapability.Embeddings,
                            LlmTckProviderCapability.Images,
                        ],
                    },
                    new()
                    {
                        Id = "invokeModelWithResponseStream",
                        Method = "POST",
                        Path = "/model/{modelId}/invoke-with-response-stream",
                        DocumentationUrl =
                            "https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_InvokeModelWithResponseStream.html",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.StreamingChat],
                    },
                ],
            },
        };
}

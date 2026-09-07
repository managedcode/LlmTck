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
            DefaultEndpointPath = "/bedrock/model/{modelId}/converse",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
                LlmTckProviderCapability.Embeddings,
                LlmTckProviderCapability.Images,
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
                DocumentationRetrievedOn = "2026-09-07",
                DocumentationVersion = "bedrock-runtime",
                Operations =
                [
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Bedrock.Converse,
                        Method = "POST",
                        Path = "/bedrock/model/{modelId}/converse",
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
                        Id = LlmTckProviderOperationIds.Bedrock.ConverseStream,
                        Method = "POST",
                        Path = "/bedrock/model/{modelId}/converse-stream",
                        DocumentationUrl =
                            "https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_ConverseStream.html",
                        SupportsStreaming = true,
                        ImplementedByHosting = true,
                        Capabilities = [LlmTckProviderCapability.StreamingChat, LlmTckProviderCapability.Tools, LlmTckProviderCapability.StructuredOutput],
                    },
                    new()
                    {
                        Id = LlmTckProviderOperationIds.Bedrock.InvokeModel,
                        Method = "POST",
                        Path = "/bedrock/model/{modelId}/invoke",
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
                        Id = LlmTckProviderOperationIds.Bedrock.InvokeModelWithResponseStream,
                        Method = "POST",
                        Path = "/bedrock/model/{modelId}/invoke-with-response-stream",
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

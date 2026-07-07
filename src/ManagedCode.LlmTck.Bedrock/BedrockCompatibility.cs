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
        };
}

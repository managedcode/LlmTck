using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Bedrock;

public static class BedrockCompatibility
{
    public const string ProviderId = "bedrock";

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
            CompatibilityTags = ["amazon-bedrock", "aws-bedrock", "bedrock"],
        };
}

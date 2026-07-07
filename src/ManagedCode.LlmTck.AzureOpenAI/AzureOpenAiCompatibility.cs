using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.AzureOpenAI;

public static class AzureOpenAiCompatibility
{
    public const string ProviderId = "azure-openai";

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
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = ["azure-openai", "openai-compatible", "foundry"],
        };
}

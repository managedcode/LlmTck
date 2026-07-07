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
                LlmTckProviderCapability.Images,
                LlmTckProviderCapability.Audio,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.AzureAIFoundry,
                LlmTckCompatibilityTags.OpenAICompatible,
            ],
        };
}

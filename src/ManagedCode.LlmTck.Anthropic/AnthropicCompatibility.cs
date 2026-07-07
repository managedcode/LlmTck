using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Anthropic;

public static class AnthropicCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Anthropic;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Anthropic",
            Protocol = LlmTckProtocolFamily.AnthropicMessages,
            DefaultEndpointPath = "/v1/messages",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags =
            [
                ProviderId,
                LlmTckCompatibilityTags.Claude,
                LlmTckCompatibilityTags.MessagesApi,
            ],
        };
}

using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Groq;

public static class GroqCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.Groq;

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "Groq",
            Protocol = LlmTckProtocolFamily.Groq,
            DefaultEndpointPath = "/openai/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = [ProviderId, LlmTckCompatibilityTags.OpenAICompatible],
        };
}

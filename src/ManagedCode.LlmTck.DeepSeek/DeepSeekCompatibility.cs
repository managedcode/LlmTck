using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.DeepSeek;

public static class DeepSeekCompatibility
{
    public const string ProviderId = "deepseek";

    public static LlmTckProviderProfile Profile { get; } =
        new()
        {
            Id = ProviderId,
            DisplayName = "DeepSeek",
            Protocol = LlmTckProtocolFamily.DeepSeek,
            DefaultEndpointPath = "/v1/chat/completions",
            Capabilities =
            [
                LlmTckProviderCapability.Chat,
                LlmTckProviderCapability.StreamingChat,
                LlmTckProviderCapability.Tools,
                LlmTckProviderCapability.StructuredOutput,
            ],
            CompatibilityTags = ["deepseek", "openai-compatible"],
        };
}

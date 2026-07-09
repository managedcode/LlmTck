using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckChatRequest
{
    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public List<LlmTckMessage> Messages { get; init; } = [];

    public bool Stream { get; init; }

    public LlmTckPromptCachePolicy PromptCachePolicy { get; init; }

    public string? PromptCacheKey { get; init; }
}

public enum LlmTckPromptCachePolicy
{
    None,
    OpenAiCompatible,
    Mistral,
    Anthropic,
    Gemini,
    Bedrock,
    DeepSeek,
    OpenRouter,
}

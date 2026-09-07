using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckChatRequest
{
    /// <summary>
    /// Optional caller-provided identifier copied to runtime trace events for stable correlation.
    /// </summary>
    public string? RequestId { get; init; }

    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public List<LlmTckMessage> Messages { get; init; } = [];

    public bool Stream { get; init; }
    public List<LlmTckToolDefinition> Tools { get; init; } = [];
    public LlmTckToolChoice ToolChoice { get; init; }
    /// <summary>Whether a fixture may contain more than one tool call in a response.</summary>
    public bool AllowParallelToolCalls { get; init; } = true;
    public string? RequiredToolName { get; init; }
    public bool RequireJson { get; init; }
    public string? ResponseSchemaJson { get; init; }

    /// <summary>Warms the prompt cache without consuming a configured response.</summary>
    public bool PopulateCacheOnly { get; init; }

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
    Ollama,
}

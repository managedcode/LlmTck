using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiResponseRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("input")]
    public JsonElement Input { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("prompt_cache_key")]
    public string? PromptCacheKey { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}

public sealed record OpenAiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "response";

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("output")]
    public List<OpenAiResponseOutputMessage> Output { get; init; } = [];

    [JsonPropertyName("usage")]
    public OpenAiResponseUsage Usage { get; init; } = new();

    [JsonPropertyName("status")]
    public string Status { get; init; } = "completed";
}

public sealed record OpenAiResponseOutputMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "completed";

    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    [JsonPropertyName("content")]
    public List<OpenAiResponseOutputText> Content { get; init; } = [];
}

public sealed record OpenAiResponseOutputText
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "output_text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("annotations")]
    public List<object> Annotations { get; init; } = [];
}

public sealed record OpenAiResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("input_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiInputTokensDetails? InputTokensDetails { get; init; }

    [JsonPropertyName("output_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiOutputTokensDetails? OutputTokensDetails { get; init; }
}

public sealed record OpenAiInputTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; init; }

    [JsonPropertyName("cache_write_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheWriteTokens { get; init; }
}

public sealed record OpenAiOutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
}

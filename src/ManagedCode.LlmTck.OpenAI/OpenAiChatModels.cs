using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    [JsonPropertyName("messages")]
    public List<OpenAiChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("prompt_cache_key")]
    public string? PromptCacheKey { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}

public sealed record OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("content")]
    public JsonElement Content { get; init; } = JsonSerializer.SerializeToElement(string.Empty);

    [JsonIgnore]
    public string TextContent => ReadTextContent(Content);

    public static OpenAiChatMessage FromText(string role, string content)
    {
        return new()
        {
            Role = role,
            Content = JsonSerializer.SerializeToElement(content),
        };
    }

    private static string ReadTextContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Concat(content.EnumerateArray().Select(ReadTextContent)),
            JsonValueKind.Object when content.TryGetProperty("text", out var text) => ReadTextContent(text),
            JsonValueKind.Object when content.TryGetProperty("content", out var nested) => ReadTextContent(nested),
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            _ => content.ToString(),
        };
    }
}

public sealed record OpenAiChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChatChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public OpenAiUsage Usage { get; init; } = new();
}

public sealed record OpenAiChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public OpenAiChatMessage Message { get; init; } = new() { Role = "assistant" };

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = "stop";
}

public sealed record OpenAiChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChatChunkChoice> Choices { get; init; } = [];
}

public sealed record OpenAiChatChunkChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("delta")]
    public OpenAiChatMessage Delta { get; init; } = new() { Role = "assistant" };

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public sealed record OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("prompt_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiPromptTokensDetails? PromptTokensDetails { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiCompletionTokensDetails? CompletionTokensDetails { get; init; }

    [JsonPropertyName("prompt_cache_hit_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PromptCacheHitTokens { get; init; }

    [JsonPropertyName("prompt_cache_miss_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PromptCacheMissTokens { get; init; }
}

public sealed record OpenAiPromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; init; }

    [JsonPropertyName("cache_write_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheWriteTokens { get; init; }
}

public sealed record OpenAiCompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
}

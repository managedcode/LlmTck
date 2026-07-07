using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "llm-tck-chat";

    [JsonPropertyName("messages")]
    public List<OpenAiChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

public sealed record OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
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

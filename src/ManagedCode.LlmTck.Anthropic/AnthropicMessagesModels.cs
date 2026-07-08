using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.Anthropic;

public sealed record AnthropicMessagesRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("messages")]
    public List<AnthropicInputMessage> Messages { get; init; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("system")]
    public JsonElement System { get; init; }
}

public sealed record AnthropicInputMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement Content { get; init; }

    [JsonIgnore]
    public string TextContent => AnthropicContentReader.ReadTextContent(Content);
}

public sealed record AnthropicMessageResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; init; } = [];

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; } = "end_turn";

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }

    [JsonPropertyName("usage")]
    public AnthropicUsage Usage { get; init; } = new();
}

public sealed record AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

public sealed record AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "error";

    [JsonPropertyName("error")]
    public AnthropicError Error { get; init; } = new();

    [JsonPropertyName("request_id")]
    public string RequestId { get; init; } = string.Empty;
}

public sealed record AnthropicError
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "invalid_request_error";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

internal static class AnthropicContentReader
{
    public static string ReadTextContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Concat(content.EnumerateArray().Select(ReadContentBlock)),
            JsonValueKind.Object => ReadContentBlock(content),
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            _ => content.ToString(),
        };
    }

    private static string ReadContentBlock(JsonElement block)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            return ReadTextContent(block);
        }

        if (block.TryGetProperty("type", out var type)
            && string.Equals(type.GetString(), "text", StringComparison.Ordinal)
            && block.TryGetProperty("text", out var text))
        {
            return ReadTextContent(text);
        }

        return block.TryGetProperty("content", out var nested)
            ? ReadTextContent(nested)
            : block.ToString();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.Bedrock;

public sealed record BedrockConverseRequest
{
    [JsonPropertyName("messages")]
    public List<BedrockMessage> Messages { get; init; } = [];

    [JsonPropertyName("system")]
    public List<BedrockContentBlock> System { get; init; } = [];
}

public sealed record BedrockMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public List<BedrockContentBlock> Content { get; init; } = [];
}

public sealed record BedrockContentBlock
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Image { get; init; }

    [JsonPropertyName("document")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Document { get; init; }
}

public sealed record BedrockConverseResponse
{
    [JsonPropertyName("output")]
    public BedrockConverseOutput Output { get; init; } = new();

    [JsonPropertyName("stopReason")]
    public string StopReason { get; init; } = "end_turn";

    [JsonPropertyName("usage")]
    public BedrockUsage Usage { get; init; } = new();

    [JsonPropertyName("metrics")]
    public BedrockMetrics Metrics { get; init; } = new();
}

public sealed record BedrockConverseOutput
{
    [JsonPropertyName("message")]
    public BedrockMessage Message { get; init; } = new();
}

public sealed record BedrockUsage
{
    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; init; }
}

public sealed record BedrockMetrics
{
    [JsonPropertyName("latencyMs")]
    public int LatencyMs { get; init; }
}

public sealed record BedrockTitanTextResponse
{
    [JsonPropertyName("inputTextTokenCount")]
    public int InputTextTokenCount { get; init; }

    [JsonPropertyName("results")]
    public IReadOnlyList<BedrockTitanTextResult> Results { get; init; } = [];
}

public sealed record BedrockTitanTextResult
{
    [JsonPropertyName("tokenCount")]
    public int TokenCount { get; init; }

    [JsonPropertyName("outputText")]
    public string OutputText { get; init; } = string.Empty;

    [JsonPropertyName("completionReason")]
    public string CompletionReason { get; init; } = "FINISHED";
}

public sealed record BedrockTitanEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public IReadOnlyList<float> Embedding { get; init; } = [];

    [JsonPropertyName("inputTextTokenCount")]
    public int InputTextTokenCount { get; init; }

    [JsonPropertyName("embeddingsByType")]
    public IReadOnlyDictionary<string, IReadOnlyList<float>> EmbeddingsByType { get; init; } =
        new Dictionary<string, IReadOnlyList<float>>(StringComparer.Ordinal);
}

public sealed record BedrockImageResponse
{
    [JsonPropertyName("images")]
    public IReadOnlyList<string> Images { get; init; } = [];

    [JsonPropertyName("finish_reasons")]
    public IReadOnlyList<string?> FinishReasons { get; init; } = [];
}

public sealed record BedrockErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.SystemOne;

public static class SystemOneDecisionTypes
{
    public const string Predicate = "noul";
    public const string Choice = "choice";
    public const string Score = "score";
}

public sealed record SystemOneDecisionRequest
{
    [JsonPropertyName("model")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Model { get; init; }
    [JsonPropertyName("state")] public JsonElement State { get; init; }
    [JsonPropertyName("questions")] public Dictionary<string, SystemOneDecisionQuestion> Questions { get; init; } = [];
}

public sealed record SystemOneDecisionQuestion
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("instructions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public JsonElement Instructions { get; init; }
    [JsonPropertyName("criteria")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public JsonElement Criteria { get; init; }
}

public sealed record SystemOneDecisionResponse
{
    [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
    [JsonPropertyName("answers")] public Dictionary<string, SystemOneDecisionAnswer> Answers { get; init; } = [];
    [JsonPropertyName("usage")] public SystemOneDecisionUsage Usage { get; init; } = new();
    [JsonPropertyName("latency_ms")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? LatencyMilliseconds { get; init; }
    [JsonPropertyName("truncated")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Truncated { get; init; }
}

public sealed record SystemOneDecisionAnswer
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("noul")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Probability { get; init; }
    [JsonPropertyName("choice")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Choice { get; init; }
    [JsonPropertyName("score")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Score { get; init; }
    [JsonPropertyName("confidence")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Confidence { get; init; }
    [JsonPropertyName("probabilities")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, double>? Probabilities { get; init; }
    [JsonPropertyName("legend")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, JsonElement>? Legend { get; init; }
}

public sealed record SystemOneDecisionUsage
{
    [JsonPropertyName("input_tokens")] public int InputTokens { get; init; }
    [JsonPropertyName("output_tokens")] public int OutputTokens { get; init; }
    [JsonPropertyName("state_tokens")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? StateTokens { get; init; }
    [JsonPropertyName("state_tokens_used")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? StateTokensUsed { get; init; }
}

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckScenarioResponse
{
    public List<LlmTckToolCall> ToolCalls { get; init; } = [];

    public string Content { get; init; } = string.Empty;

    public List<string> StreamChunks { get; init; } = [];

    public LlmTckScenarioError? Error { get; init; }

    public int DelayMilliseconds { get; init; }
}

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckMessage
{
    public string? ToolCallId { get; init; }

    public string Role { get; init; } = "user";

    public List<LlmTckToolCall> ToolCalls { get; init; } = [];

    public string Content { get; init; } = string.Empty;
}

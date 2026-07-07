namespace ManagedCode.LlmTck.Runtime;

public sealed record LlmTckRuntimeEvent
{
    public DateTimeOffset Timestamp { get; init; }

    public LlmTckEventKind Kind { get; init; }

    public string? ScenarioId { get; init; }

    public string ModelId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

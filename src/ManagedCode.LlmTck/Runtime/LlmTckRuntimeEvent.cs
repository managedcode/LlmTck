namespace ManagedCode.LlmTck.Runtime;

public sealed record LlmTckRuntimeEvent
{
    public DateTimeOffset Timestamp { get; init; }

    public LlmTckEventKind Kind { get; init; }

    public string? ScenarioId { get; init; }

    public string ModelId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Readable preview of the incoming request (chat roles and content) that produced this event,
    /// when one is available. Lets operators see exactly what was asked.
    /// </summary>
    public string? Request { get; init; }

    /// <summary>
    /// The response the model returned for this event (matched content or the error text),
    /// when one is available. Lets operators see exactly what the model answered.
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// Deterministic token usage observed for this event, when the runtime can derive it.
    /// </summary>
    public LlmTckTokenUsage? Usage { get; init; }
}

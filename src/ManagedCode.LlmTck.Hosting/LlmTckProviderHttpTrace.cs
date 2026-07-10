using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Hosting;

public sealed record LlmTckProviderHttpTrace
{
    public string RequestId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string ProviderNamespace { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string? Route { get; init; }

    public string? OperationId { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public double? DurationMilliseconds { get; init; }

    public int? StatusCode { get; init; }

    public bool IsCompleted { get; init; }

    public LlmTckProviderHttpTraceTransportState TransportState { get; init; }

    public bool? IsSuccess => !IsCompleted
        ? null
        : TransportState != LlmTckProviderHttpTraceTransportState.Completed
            ? false
            : StatusCode is null
                ? null
                : StatusCode < 400;

    public bool IsStreaming { get; init; }

    public string? RequestContentType { get; init; }

    public long? RequestByteCount { get; init; }

    public string? RequestPreview { get; init; }

    public bool RequestPreviewTruncated { get; init; }

    public string? ResponseContentType { get; init; }

    public long ResponseByteCount { get; init; }

    public string? ResponsePreview { get; init; }

    public bool ResponsePreviewTruncated { get; init; }

    public string? ModelId { get; init; }

    public string? ScenarioId { get; init; }

    public LlmTckEventKind? RuntimeEventKind { get; init; }

    public string? RuntimeMessage { get; init; }

    public string? RuntimeResponse { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? TransportError { get; init; }

    public LlmTckTokenUsage? Usage { get; init; }

    public IReadOnlyList<LlmTckMessage> Messages { get; init; } = [];

    public IReadOnlyList<string> StreamChunks { get; init; } = [];

    public bool RuntimePayloadTruncated { get; init; }

    public bool RetentionTruncated { get; init; }

    public int RetainedByteCount { get; init; }
}

public sealed record LlmTckProviderHttpTraceCompletion
{
    public DateTimeOffset CompletedAt { get; init; }

    public double DurationMilliseconds { get; init; }

    public int? StatusCode { get; init; }

    public LlmTckProviderHttpTraceTransportState TransportState { get; init; }

    public string? Route { get; init; }

    public string? OperationId { get; init; }

    public bool IsStreaming { get; init; }

    public string? ResponseContentType { get; init; }

    public long ResponseByteCount { get; init; }

    public string? ResponsePreview { get; init; }

    public bool ResponsePreviewTruncated { get; init; }

    public string? TransportError { get; init; }
}

public sealed record LlmTckProviderHttpTraceRequestCapture
{
    public long? ByteCount { get; init; }

    public string? Preview { get; init; }

    public bool PreviewTruncated { get; init; }

    public string? ModelId { get; init; }
}

public sealed record LlmTckProviderHttpTraceEnrichment
{
    public string? OperationId { get; init; }

    public string? ModelId { get; init; }

    public string? ScenarioId { get; init; }

    public LlmTckEventKind? RuntimeEventKind { get; init; }

    public string? RuntimeMessage { get; init; }

    public string? RuntimeResponse { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public LlmTckTokenUsage? Usage { get; init; }

    public IReadOnlyList<LlmTckMessage>? Messages { get; init; }

    public IReadOnlyList<string>? StreamChunks { get; init; }

    public bool? IsStreaming { get; init; }

    public bool? RuntimePayloadTruncated { get; init; }
}

public sealed class LlmTckProviderHttpTraceOptions
{
    public const int DefaultMaxEntries = 500;
    public const int DefaultMaxPreviewBytes = 64 * 1024;
    public const int DefaultMaxRetainedBytes = 16 * 1024 * 1024;
    public const string DefaultRequestIdHeaderName = "x-llm-tck-request-id";

    public int MaxEntries { get; set; } = DefaultMaxEntries;

    public int MaxPreviewBytes { get; set; } = DefaultMaxPreviewBytes;

    public int MaxRetainedBytes { get; set; } = DefaultMaxRetainedBytes;

    public string RequestIdHeaderName { get; set; } = DefaultRequestIdHeaderName;
}

public interface ILlmTckProviderHttpTraceStore
{
    long RetainedByteCount { get; }

    IReadOnlyList<LlmTckProviderHttpTrace> GetSnapshot();

    void Start(LlmTckProviderHttpTrace trace);

    bool TryCaptureRequest(string requestId, LlmTckProviderHttpTraceRequestCapture capture);

    bool TryComplete(string requestId, LlmTckProviderHttpTraceCompletion completion);

    bool TryEnrich(string requestId, LlmTckProviderHttpTraceEnrichment enrichment);

    void Reset();
}

public enum LlmTckProviderHttpTraceTransportState
{
    InProgress,
    Completed,
    Cancelled,
    Aborted,
    Faulted,
}

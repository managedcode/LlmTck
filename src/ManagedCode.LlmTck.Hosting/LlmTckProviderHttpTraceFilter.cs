using System.Diagnostics;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManagedCode.LlmTck.Hosting;

public sealed class LlmTckProviderHttpTraceFilter(
    ILlmTckProviderHttpTraceStore traceStore,
    ILlmTckRuntime runtime,
    IOptions<LlmTckProviderHttpTraceOptions> options,
    ILogger<LlmTckProviderHttpTraceFilter> logger
) : IEndpointFilter
{
    private static readonly ProviderRoute[] _providerRoutes =
    [
        new(LlmTckProviderRouteNamespaces.Anthropic, LlmTckCompatibilityTags.Anthropic),
        new(LlmTckProviderRouteNamespaces.AzureOpenAI, LlmTckCompatibilityTags.AzureOpenAI),
        new(LlmTckProviderRouteNamespaces.Bedrock, LlmTckCompatibilityTags.Bedrock),
        new(LlmTckProviderRouteNamespaces.Cohere, LlmTckCompatibilityTags.Cohere),
        new(LlmTckProviderRouteNamespaces.DeepSeek, LlmTckCompatibilityTags.DeepSeek),
        new(LlmTckProviderRouteNamespaces.Gemini, LlmTckCompatibilityTags.Gemini),
        new(LlmTckProviderRouteNamespaces.Groq, LlmTckCompatibilityTags.Groq),
        new(
            LlmTckProviderRouteNamespaces.MicrosoftFoundry,
            LlmTckCompatibilityTags.MicrosoftFoundry
        ),
        new(LlmTckProviderRouteNamespaces.Mistral, LlmTckCompatibilityTags.Mistral),
        new(LlmTckProviderRouteNamespaces.Ollama, LlmTckCompatibilityTags.Ollama),
        new(LlmTckProviderRouteNamespaces.OpenAI, LlmTckCompatibilityTags.OpenAI),
        new(LlmTckProviderRouteNamespaces.OpenRouter, LlmTckCompatibilityTags.OpenRouter),
        new(LlmTckProviderRouteNamespaces.Perplexity, LlmTckCompatibilityTags.Perplexity),
    ];

    private readonly LlmTckProviderHttpTraceOptions _options = options.Value;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var provider = FindProvider(httpContext.Request.Path);
        if (provider is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var session = TryStartSession(httpContext, provider.Value);
        if (session is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var runtimeScope = TryBeginRuntimeScope(session.RequestId);
        try
        {
            object? result;
            try
            {
                result = await next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                session.CaptureRequestBestEffort();
                await session.CompleteBestEffortAsync(exception).ConfigureAwait(false);
                throw;
            }

            session.CaptureRequestBestEffort();
            if (result is IResult httpResult)
            {
                return new LlmTckProviderHttpTraceResult(httpResult, session);
            }

            session.RegisterCompletionFallback();
            return result;
        }
        finally
        {
            DisposeRuntimeScopeBestEffort(runtimeScope, session.RequestId);
        }
    }

    private TraceSession? TryStartSession(HttpContext context, ProviderRoute provider)
    {
        var requestId = context.TraceIdentifier;
        var requestContentType = LlmTckProviderHttpTracePayloads.NormalizeContentType(
            context.Request.ContentType
        );
        var requestCaptureLimit = LlmTckProviderHttpTracePayloads.ShouldCaptureBody(
            requestContentType
        )
            ? _options.MaxPreviewBytes
            : 0;
        var originalRequestBody = context.Request.Body;
        var originalResponseBody = context.Response.Body;
        var requestTee = new LlmTckBoundedReadTeeStream(
            originalRequestBody,
            requestCaptureLimit
        );
        var responseTee = new LlmTckBoundedWriteTeeStream(
            originalResponseBody,
            _options.MaxPreviewBytes
        );
        var endpoint = context.GetEndpoint();
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        var operationId = endpoint
            ?.Metadata
            .GetMetadata<LlmTckProviderOperationMetadata>()
            ?.OperationId;

        try
        {
            context.Request.Body = requestTee;
            context.Response.Body = responseTee;
            var session = new TraceSession(
                context,
                traceStore,
                runtime,
                logger,
                requestId,
                requestContentType,
                _options.MaxPreviewBytes,
                route,
                operationId,
                originalRequestBody,
                originalResponseBody,
                requestTee,
                responseTee
            );
            LlmTckProviderHttpTraceExtensions.SetLlmTckProviderHttpTraceId(context, requestId);
            traceStore.Start(
                new LlmTckProviderHttpTrace
                {
                    RequestId = requestId,
                    ProviderId = provider.ProviderId,
                    ProviderNamespace = provider.Namespace,
                    Method = context.Request.Method,
                    Path = context.Request.PathBase.Add(context.Request.Path).Value ?? string.Empty,
                    Route = route,
                    OperationId = operationId,
                    StartedAt = session.StartedAt,
                    TransportState = LlmTckProviderHttpTraceTransportState.InProgress,
                    RequestContentType = requestContentType,
                    RequestByteCount = context.Request.ContentLength,
                    ModelId = ReadRouteModelId(context),
                }
            );
            TrySetRequestIdHeader(context, requestId);
            return session;
        }
        catch (Exception exception)
        {
            TryRestoreBody(context, originalRequestBody, originalResponseBody);
            TryDispose(requestTee);
            TryDispose(responseTee);
            LogFailure(exception, "start", requestId);
            return null;
        }
    }

    private IDisposable? TryBeginRuntimeScope(string requestId)
    {
        if (runtime is not ILlmTckRuntimeRequestScope runtimeRequestScope)
        {
            return null;
        }

        try
        {
            return runtimeRequestScope.BeginRequest(requestId);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "runtime correlation start", requestId);
            return null;
        }
    }

    private void DisposeRuntimeScopeBestEffort(IDisposable? runtimeScope, string requestId)
    {
        if (runtimeScope is null)
        {
            return;
        }

        try
        {
            runtimeScope.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "runtime correlation disposal", requestId);
        }
    }

    private void TrySetRequestIdHeader(HttpContext context, string requestId)
    {
        try
        {
            context.Response.Headers[_options.RequestIdHeaderName] = requestId;
        }
        catch (Exception exception)
        {
            LogFailure(exception, "request id response header", requestId);
        }
    }

    private void LogFailure(Exception exception, string phase, string requestId)
    {
        logger.LogWarning(
            exception,
            "LLM TCK provider HTTP tracing failed during {Phase} for request {RequestId}; provider behavior continues unchanged.",
            phase,
            requestId
        );
    }

    private static void TryRestoreBody(
        HttpContext context,
        Stream requestBody,
        Stream responseBody
    )
    {
        try
        {
            context.Request.Body = requestBody;
        }
        catch (Exception)
        {
        }

        try
        {
            context.Response.Body = responseBody;
        }
        catch (Exception)
        {
        }
    }

    private static void TryDispose(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private static ProviderRoute? FindProvider(PathString path)
    {
        foreach (var provider in _providerRoutes)
        {
            if (
                path.Equals(provider.Namespace, StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments(provider.Namespace, StringComparison.OrdinalIgnoreCase)
            )
            {
                return provider;
            }
        }

        return null;
    }

    private static string? ReadRouteModelId(HttpContext context)
    {
        return ReadRouteValue(context, "model")
            ?? ReadRouteValue(context, "modelId")
            ?? ReadRouteValue(context, "deployment");
    }

    private static string? ReadRouteValue(HttpContext context, string name)
    {
        return context.Request.RouteValues.TryGetValue(name, out var value)
            && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()
            : null;
    }

    private sealed class TraceSession(
        HttpContext context,
        ILlmTckProviderHttpTraceStore traceStore,
        ILlmTckRuntime runtime,
        ILogger logger,
        string requestId,
        string? requestContentType,
        int maxPreviewBytes,
        string? route,
        string? operationId,
        Stream originalRequestBody,
        Stream originalResponseBody,
        LlmTckBoundedReadTeeStream requestTee,
        LlmTckBoundedWriteTeeStream responseTee
    )
    {
        private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
        private int _requestCaptured;
        private int _completed;

        public string RequestId => requestId;

        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

        public void CaptureRequestBestEffort()
        {
            if (Interlocked.Exchange(ref _requestCaptured, 1) != 0)
            {
                return;
            }

            RestoreRequestBodyBestEffort();
            try
            {
                var observedBytes = requestTee.BytesObserved;
                var expectedBytes = context.Request.ContentLength;
                var incomplete = expectedBytes is > 0 && observedBytes < expectedBytes;
                var capturedBytes = requestTee.GetCapturedBytes();
                var preview = TryCreateMultipartPreview(expectedBytes ?? observedBytes)
                    ?? LlmTckProviderHttpTracePayloads.CreateRequestPreview(
                        capturedBytes,
                        requestContentType,
                        expectedBytes ?? observedBytes,
                        requestTee.IsTruncated || incomplete
                    );
                traceStore.TryCaptureRequest(
                    requestId,
                    new LlmTckProviderHttpTraceRequestCapture
                    {
                        ByteCount = preview.ByteCount,
                        Preview = preview.Preview,
                        PreviewTruncated = preview.IsTruncated,
                        ModelId = preview.ModelId,
                    }
                );

                if (requestTee.CaptureFailed)
                {
                    LogCaptureFailure("request body capture");
                }
            }
            catch (Exception exception)
            {
                LogFailure(exception, "request capture");
            }
            finally
            {
                DisposeBestEffort(requestTee, "request capture disposal");
            }
        }

        private LlmTckProviderHttpPayloadPreview? TryCreateMultipartPreview(long? byteCount)
        {
            if (
                requestContentType?.StartsWith(
                    "multipart/",
                    StringComparison.OrdinalIgnoreCase
                ) != true
            )
            {
                return null;
            }

            try
            {
                var form = context.Features.Get<IFormFeature>()?.Form;
                return form is null
                    ? null
                    : LlmTckProviderHttpTracePayloads.CreateMultipartRequestPreview(
                        form,
                        requestContentType,
                        byteCount,
                        maxPreviewBytes
                    );
            }
            catch (Exception exception)
            {
                LogFailure(exception, "cached multipart form summary");
                return null;
            }
        }

        public void RegisterCompletionFallback()
        {
            try
            {
                context.Response.OnCompleted(
                    static state => ((TraceSession)state).CompleteBestEffortAsync(null),
                    this
                );
            }
            catch (Exception exception)
            {
                LogFailure(exception, "completion callback registration");
                _ = CompleteBestEffortAsync(null);
            }
        }

        public Task CompleteBestEffortAsync(Exception? exception)
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return Task.CompletedTask;
            }

            CaptureRequestBestEffort();
            RestoreResponseBodyBestEffort();

            var transportState = ClassifyTransportState(context, exception);
            int? statusCode = transportState == LlmTckProviderHttpTraceTransportState.Completed
                || context.Response.HasStarted
                ? context.Response.StatusCode
                : null;
            EnrichFromRuntimeEventBestEffort();

            try
            {
                var responseContentType = LlmTckProviderHttpTracePayloads.NormalizeContentType(
                    context.Response.ContentType
                );
                var responseBytes = responseTee.GetCapturedBytes();
                traceStore.TryComplete(
                    requestId,
                    new LlmTckProviderHttpTraceCompletion
                    {
                        CompletedAt = DateTimeOffset.UtcNow,
                        DurationMilliseconds = Stopwatch
                            .GetElapsedTime(_startedTimestamp)
                            .TotalMilliseconds,
                        StatusCode = statusCode,
                        TransportState = transportState,
                        Route = route,
                        OperationId = operationId,
                        IsStreaming = LlmTckProviderHttpTracePayloads.IsStreamingResponse(
                            responseContentType,
                            route,
                            responseTee.SuccessfulFlushCount
                        ),
                        ResponseContentType = responseContentType,
                        ResponseByteCount = responseTee.BytesObserved,
                        ResponsePreview = LlmTckProviderHttpTracePayloads.CreateResponsePreview(
                            responseBytes,
                            responseContentType,
                            responseTee.BytesObserved,
                            responseTee.IsTruncated
                        ),
                        ResponsePreviewTruncated = responseTee.IsTruncated,
                        TransportError = exception?.GetType().Name
                            ?? (transportState == LlmTckProviderHttpTraceTransportState.Aborted
                                ? "RequestAborted"
                                : null),
                    }
                );

                if (responseTee.CaptureFailed)
                {
                    LogCaptureFailure("response body capture");
                }
            }
            catch (Exception finalizationException)
            {
                LogFailure(finalizationException, "finalization");
                TryCompleteWithoutPayload(transportState, statusCode, exception);
            }
            finally
            {
                DisposeBestEffort(responseTee, "response capture disposal");
            }

            return Task.CompletedTask;
        }

        private void EnrichFromRuntimeEventBestEffort()
        {
            try
            {
                if (runtime is not ILlmTckRuntimeEventLookup lookup
                    || !lookup.TryGetEvent(requestId, out var runtimeEvent)
                    || runtimeEvent is null)
                {
                    return;
                }

                traceStore.TryEnrich(
                    requestId,
                    new LlmTckProviderHttpTraceEnrichment
                    {
                        ModelId = runtimeEvent.ModelId,
                        ScenarioId = runtimeEvent.ScenarioId,
                        RuntimeEventKind = runtimeEvent.Kind,
                        RuntimeMessage = runtimeEvent.Message,
                        RuntimeResponse = runtimeEvent.Response,
                        Usage = runtimeEvent.Usage,
                        Messages = runtimeEvent.Messages,
                        StreamChunks = runtimeEvent.StreamChunks,
                        IsStreaming = runtimeEvent.IsStreaming,
                        RuntimePayloadTruncated = runtimeEvent.PayloadTruncated,
                    }
                );
            }
            catch (Exception exception)
            {
                LogFailure(exception, "runtime event enrichment");
            }
        }

        private void TryCompleteWithoutPayload(
            LlmTckProviderHttpTraceTransportState transportState,
            int? statusCode,
            Exception? exception
        )
        {
            try
            {
                traceStore.TryComplete(
                    requestId,
                    new LlmTckProviderHttpTraceCompletion
                    {
                        CompletedAt = DateTimeOffset.UtcNow,
                        DurationMilliseconds = Stopwatch
                            .GetElapsedTime(_startedTimestamp)
                            .TotalMilliseconds,
                        StatusCode = statusCode,
                        TransportState = transportState,
                        Route = route,
                        OperationId = operationId,
                        ResponseContentType = LlmTckProviderHttpTracePayloads.NormalizeContentType(
                            context.Response.ContentType
                        ),
                        ResponseByteCount = responseTee.BytesObserved,
                        ResponsePreviewTruncated = responseTee.IsTruncated,
                        TransportError = exception?.GetType().Name,
                    }
                );
            }
            catch (Exception fallbackException)
            {
                LogFailure(fallbackException, "fallback finalization");
            }
        }

        private void RestoreRequestBodyBestEffort()
        {
            try
            {
                context.Request.Body = originalRequestBody;
            }
            catch (Exception exception)
            {
                LogFailure(exception, "request stream restoration");
            }
        }

        private void RestoreResponseBodyBestEffort()
        {
            try
            {
                context.Response.Body = originalResponseBody;
            }
            catch (Exception exception)
            {
                LogFailure(exception, "response stream restoration");
            }
        }

        private void DisposeBestEffort(IDisposable disposable, string phase)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                LogFailure(exception, phase);
            }
        }

        private void LogCaptureFailure(string phase)
        {
            logger.LogWarning(
                "LLM TCK provider HTTP tracing could not retain all bytes during {Phase} for request {RequestId}; provider behavior was not affected.",
                phase,
                requestId
            );
        }

        private void LogFailure(Exception exception, string phase)
        {
            logger.LogWarning(
                exception,
                "LLM TCK provider HTTP tracing failed during {Phase} for request {RequestId}; provider behavior continues unchanged.",
                phase,
                requestId
            );
        }

        private static LlmTckProviderHttpTraceTransportState ClassifyTransportState(
            HttpContext context,
            Exception? exception
        )
        {
            if (
                context.RequestAborted.IsCancellationRequested
                || exception is ConnectionAbortedException
            )
            {
                return LlmTckProviderHttpTraceTransportState.Aborted;
            }

            if (exception is OperationCanceledException)
            {
                return LlmTckProviderHttpTraceTransportState.Cancelled;
            }

            return exception is null
                ? LlmTckProviderHttpTraceTransportState.Completed
                : LlmTckProviderHttpTraceTransportState.Faulted;
        }
    }

    private sealed class LlmTckProviderHttpTraceResult(
        IResult result,
        TraceSession session
    ) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            Exception? exception = null;
            try
            {
                await result.ExecuteAsync(httpContext).ConfigureAwait(false);
            }
            catch (Exception caught)
            {
                exception = caught;
                throw;
            }
            finally
            {
                await session.CompleteBestEffortAsync(exception).ConfigureAwait(false);
            }
        }
    }

    private readonly record struct ProviderRoute(string Namespace, string ProviderId);
}

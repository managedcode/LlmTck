using System.Security.Cryptography;
using System.Text;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed class LlmTckRuntime
    : ILlmTckRuntime,
        ILlmTckRuntimeRequestScope,
        ILlmTckRuntimeEventLookup
{
    private const string _tooManyRequestsCode = "too_many_requests";
    private const string _contentFilterCode = "content_filter";
    private const string _requestSupersededCode = "llm_tck_request_superseded";
    private const string _requestSupersededMessage =
        "The runtime was reset or reconfigured while the request was in flight.";
    private const int _openAiPromptCacheMinimumTokens = 1024;
    private const int _openAiPromptCacheIncrementTokens = 128;
    private const int _mistralPromptCacheMinimumTokens = 64;
    private const int _mistralPromptCacheIncrementTokens = 64;
    private const int _geminiPromptCacheMinimumTokens = 2048;
    private const int _geminiPromptCacheIncrementTokens = 128;
    private const int _maxRetainedEvents = 500;
    private const int _maxRetainedEventCharacters = 8 * 1024 * 1024;
    private const int _maxRetainedMetadataCharacters = 4096;
    private const int _retainedMessageCost = 256;
    private const int _retainedChunkCost = 128;

    private readonly object _gate = new();
    private readonly AsyncLocal<RuntimeRequestContext?> _activeRequest = new();
    private readonly Dictionary<string, int> _scenarioPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _promptCacheEntries = new(StringComparer.Ordinal);
    private readonly Queue<LlmTckRuntimeEvent> _events = [];
    private readonly Dictionary<string, LlmTckRuntimeEvent> _eventsByRequestId = new(
        StringComparer.Ordinal
    );
    private readonly int[] _eventKindCounts = new int[Enum.GetValues<LlmTckEventKind>().Length];
    private LlmTckConfiguration _configuration = LlmTckConfiguration.CreateDefault();
    private int _requestCount;
    private int _totalEvents;
    private int _inputTokens;
    private int _cachedInputTokens;
    private int _cacheCreationInputTokens;
    private int _outputTokens;
    private int _reasoningTokens;
    private int _totalTokens;
    private int _retainedEventCharacters;
    private long _generation;

    public LlmTckRuntime()
    {
    }

    public LlmTckRuntime(LlmTckConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = LlmTckConfigurationBuilder.Snapshot(configuration);
    }

    public Task ConfigureAsync(
        LlmTckConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _configuration = LlmTckConfigurationBuilder.Snapshot(configuration);
            _scenarioPositions.Clear();
            _promptCacheEntries.Clear();
            ResetEventJournal();
            _requestCount = 0;
            _generation++;
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _scenarioPositions.Clear();
            _promptCacheEntries.Clear();
            ResetEventJournal();
            _requestCount = 0;
            _generation++;
        }

        return Task.CompletedTask;
    }

    public bool IsBearerTokenAccepted(string? bearerToken)
    {
        lock (_gate)
        {
            return HasRequiredToken(_configuration.RequiredBearerToken, bearerToken);
        }
    }

    public IReadOnlyList<LlmTckModel> GetModels()
    {
        lock (_gate)
        {
            return [.. _configuration.Models];
        }
    }

    public IDisposable BeginRequest(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        var previousRequest = _activeRequest.Value;
        lock (_gate)
        {
            _activeRequest.Value = new RuntimeRequestContext(requestId, _generation);
        }

        return new RuntimeRequestScope(_activeRequest, previousRequest);
    }

    public bool TryGetEvent(string requestId, out LlmTckRuntimeEvent? runtimeEvent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        lock (_gate)
        {
            return _eventsByRequestId.TryGetValue(requestId, out runtimeEvent);
        }
    }

    public async Task<LlmTckChatResult> CompleteChatAsync(
        LlmTckChatRequest request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        LlmTckScenarioResponse response;
        LlmTckScenario? scenario;
        int responsePosition;
        long generation;

        lock (_gate)
        {
            generation = _activeRequest.Value?.Generation ?? _generation;
            if (generation != _generation)
            {
                return CreateSupersededChatResult(request, scenarioId: null);
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                var usage = CreateChatUsage(request);
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    request.ModelId,
                    "Global bearer token requirement failed.",
                    FormatChatRequest(request),
                    usage: usage,
                    chatRequest: request
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    401,
                    "invalid_api_key",
                    "The supplied bearer token did not match the configured LLM TCK token.",
                    usage: usage
                );
            }

            if (!IsConfiguredModel(request.ModelId, LlmTckModelKind.Chat))
            {
                var usage = CreateChatUsage(request);
                AddModelNotFoundEvent(
                    request.ModelId,
                    LlmTckModelKind.Chat,
                    FormatChatRequest(request),
                    usage,
                    chatRequest: request
                );
                return UnknownModel(request.ModelId, LlmTckModelKind.Chat, usage);
            }

            var fault = TryCreateFault(
                request.ModelId,
                request.Messages.Select(message => message.Content),
                FormatChatRequest(request),
                CreateChatUsage(request),
                chatRequest: request
            );
            if (fault is not null)
            {
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    fault.StatusCode,
                    fault.Code,
                    fault.Message,
                    usage: fault.Usage
                );
            }

            scenario = GetChatScenarios(_configuration)
                .FirstOrDefault(candidate => ScenarioMatches(candidate, request));

            if (scenario is null)
            {
                var usage = CreateChatUsage(request);
                AddEvent(
                    LlmTckEventKind.Unmatched,
                    null,
                    request.ModelId,
                    "No configured scenario matched the request.",
                    FormatChatRequest(request),
                    usage: usage,
                    chatRequest: request
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    404,
                    "llm_tck_unmatched_request",
                    "No configured LLM TCK scenario matched the request.",
                    usage: usage
                );
            }

            if (!HasRequiredToken(scenario.RequiredBearerToken, bearerToken))
            {
                var usage = CreateChatUsage(request);
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    scenario.Id,
                    request.ModelId,
                    "Scenario bearer token requirement failed.",
                    FormatChatRequest(request),
                    usage: usage,
                    chatRequest: request
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    401,
                    "invalid_api_key",
                    "The supplied bearer token did not match the scenario token.",
                    scenario.Id,
                    usage
                );
            }

            responsePosition = _scenarioPositions.GetValueOrDefault(scenario.Id);
            if (responsePosition >= scenario.Responses.Count)
            {
                var usage = CreateChatUsage(request);
                AddEvent(
                    LlmTckEventKind.ScenarioExhausted,
                    scenario.Id,
                    request.ModelId,
                    "Scenario response queue is exhausted.",
                    FormatChatRequest(request),
                    usage: usage,
                    chatRequest: request
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    409,
                    "llm_tck_scenario_exhausted",
                    "The matched scenario has no responses left.",
                    scenario.Id,
                    usage
                );
            }

            response = scenario.Responses[responsePosition];
            _scenarioPositions[scenario.Id] = responsePosition + 1;
        }

        try
        {
            if (response.DelayMilliseconds > 0)
            {
                await Task.Delay(response.DelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RollBackReservedResponse(scenario.Id, responsePosition, generation);
            throw;
        }

        lock (_gate)
        {
            if (generation != _generation)
            {
                return CreateSupersededChatResult(request, scenario.Id);
            }

            var responseText = response.Error?.Message ?? response.Content;
            var usage = CreateChatUsage(
                request,
                responseText,
                updatePromptCache: response.Error is null
            );
            IReadOnlyList<string> successfulStreamChunks = response.StreamChunks.Count > 0
                ? response.StreamChunks
                : [response.Content];
            AddEvent(
                response.Error is null ? LlmTckEventKind.Matched : LlmTckEventKind.ErrorReturned,
                scenario.Id,
                request.ModelId,
                response.Error?.Message ?? "Scenario matched.",
                FormatChatRequest(request),
                responseText,
                usage,
                chatRequest: request,
                streamChunks: response.Error is null && request.Stream ? successfulStreamChunks : null
            );

            if (response.Error is not null)
            {
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    response.Error.StatusCode,
                    response.Error.Code,
                    response.Error.Message,
                    scenario.Id,
                    usage
                );
            }

            return LlmTckChatResult.Success(
                request.ModelId,
                scenario.Id,
                response.Content,
                successfulStreamChunks,
                usage
            );
        }
    }

    public Task<LlmTckEmbeddingResult> CreateEmbeddingAsync(
        string modelId,
        IReadOnlyList<string> inputs,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(inputs);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededEmbeddingResult(modelId));
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Embedding bearer token requirement failed."
                );
                return Task.FromResult(
                    new LlmTckEmbeddingResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Embedding))
            {
                AddModelNotFoundEvent(
                    modelId,
                    LlmTckModelKind.Embedding
                );
                return Task.FromResult(UnknownEmbeddingModel(modelId));
            }

            var fault = TryCreateFault(
                modelId,
                inputs,
                request: string.Join("\n", inputs)
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckEmbeddingResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                $"Generated deterministic embeddings for {inputs.Count} input value(s)."
            );

            return Task.FromResult(
                new LlmTckEmbeddingResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    Vectors = inputs.Select(_ => _configuration.DefaultEmbeddingVector.ToList()).ToList(),
                }
            );
        }
    }

    public Task<LlmTckImageResult> GenerateImageAsync(
        string modelId,
        string prompt,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(prompt);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededImageResult(modelId));
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Image auth failed."
                );
                return Task.FromResult(
                    new LlmTckImageResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Image))
            {
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Image);
                return Task.FromResult(UnknownImageModel(modelId));
            }

            var fault = TryCreateFault(
                modelId,
                [prompt],
                request: prompt
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckImageResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                $"Generated image for '{prompt}'."
            );
            return Task.FromResult(
                new LlmTckImageResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    DataUri = _configuration.DefaultImageDataUri,
                }
            );
        }
    }

    public Task<LlmTckAudioResult> GenerateAudioAsync(
        string modelId,
        string input,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededAudioResult(modelId));
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Audio auth failed."
                );
                return Task.FromResult(
                    new LlmTckAudioResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Audio))
            {
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Audio);
                return Task.FromResult(UnknownAudioModel(modelId));
            }

            var fault = TryCreateFault(
                modelId,
                [input],
                request: input
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckAudioResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                $"Generated audio for '{input}'."
            );
            return Task.FromResult(
                new LlmTckAudioResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    Bytes = [.. _configuration.DefaultAudioBytes],
                    MediaType = _configuration.DefaultAudioMediaType,
                }
            );
        }
    }

    public Task<LlmTckTranscriptionResult> TranscribeAudioAsync(
        string modelId,
        string fileName,
        string? prompt = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededTranscriptionResult(modelId));
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Audio transcription auth failed."
                );
                return Task.FromResult(
                    new LlmTckTranscriptionResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Audio))
            {
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Audio);
                return Task.FromResult(UnknownTranscriptionModel(modelId));
            }

            var fault = TryCreateFault(
                modelId,
                [fileName, prompt ?? string.Empty],
                request: FormatAudioRequest(fileName, prompt)
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckTranscriptionResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                string.IsNullOrWhiteSpace(prompt)
                    ? $"Transcribed audio file '{fileName}'."
                    : $"Transcribed audio file '{fileName}' with prompt."
            );
            return Task.FromResult(
                new LlmTckTranscriptionResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    Text = _configuration.DefaultTranscriptionText,
                }
            );
        }
    }

    public Task<LlmTckTranscriptionResult> TranslateAudioAsync(
        string modelId,
        string fileName,
        string? prompt = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededTranscriptionResult(modelId));
            }

            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Audio translation auth failed."
                );
                return Task.FromResult(
                    new LlmTckTranscriptionResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Audio))
            {
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Audio);
                return Task.FromResult(UnknownTranscriptionModel(modelId));
            }

            var fault = TryCreateFault(
                modelId,
                [fileName, prompt ?? string.Empty],
                request: FormatAudioRequest(fileName, prompt)
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckTranscriptionResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                string.IsNullOrWhiteSpace(prompt)
                    ? $"Translated audio file '{fileName}'."
                    : $"Translated audio file '{fileName}' with prompt."
            );
            return Task.FromResult(
                new LlmTckTranscriptionResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    Text = _configuration.DefaultTranslationText,
                }
            );
        }
    }

    public Task<LlmTckVideoResult> GenerateVideoAsync(
        string modelId,
        string prompt,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(prompt);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveRequestSuperseded())
            {
                return Task.FromResult(CreateSupersededVideoResult(modelId));
            }

            var usage = CreateVideoUsage(prompt);
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    modelId,
                    "Video auth failed.",
                    usage: usage
                );
                return Task.FromResult(
                    new LlmTckVideoResult
                    {
                        IsSuccess = false,
                        StatusCode = 401,
                        ModelId = modelId,
                        ErrorCode = "invalid_api_key",
                        ErrorMessage = "The supplied bearer token did not match the configured LLM TCK token.",
                        Usage = usage,
                    }
                );
            }

            if (!IsConfiguredModel(modelId, LlmTckModelKind.Video))
            {
                AddModelNotFoundEvent(
                    modelId,
                    LlmTckModelKind.Video,
                    usage: usage
                );
                return Task.FromResult(UnknownVideoModel(modelId, usage));
            }

            var fault = TryCreateFault(
                modelId,
                [prompt],
                request: prompt,
                usage: usage
            );
            if (fault is not null)
            {
                return Task.FromResult(
                    new LlmTckVideoResult
                    {
                        IsSuccess = false,
                        StatusCode = fault.StatusCode,
                        ModelId = modelId,
                        ErrorCode = fault.Code,
                        ErrorMessage = fault.Message,
                        Usage = fault.Usage ?? usage,
                    }
                );
            }

            AddEvent(
                LlmTckEventKind.Matched,
                null,
                modelId,
                $"Generated video for '{prompt}'.",
                usage: usage
            );
            return Task.FromResult(
                new LlmTckVideoResult
                {
                    IsSuccess = true,
                    ModelId = modelId,
                    Prompt = prompt,
                    VideoId = _configuration.DefaultVideoId,
                    GenerationId = _configuration.DefaultVideoGenerationId,
                    Bytes = [.. _configuration.DefaultVideoBytes],
                    MediaType = _configuration.DefaultVideoMediaType,
                    CreatedAt = _configuration.DefaultVideoCreatedAtUnixTime,
                    Size = _configuration.DefaultVideoSize,
                    Seconds = _configuration.DefaultVideoSeconds,
                    Usage = usage,
                }
            );
        }
    }

    public LlmTckAssertionSummary GetAssertionSummary()
    {
        lock (_gate)
        {
            return new LlmTckAssertionSummary
            {
                TotalEvents = _totalEvents,
                Matched = _eventKindCounts[(int)LlmTckEventKind.Matched],
                Unmatched = _eventKindCounts[(int)LlmTckEventKind.Unmatched],
                ModelNotFound = _eventKindCounts[(int)LlmTckEventKind.ModelNotFound],
                AuthFailed = _eventKindCounts[(int)LlmTckEventKind.AuthFailed],
                ScenarioExhausted = _eventKindCounts[(int)LlmTckEventKind.ScenarioExhausted],
                ErrorsReturned = _eventKindCounts[(int)LlmTckEventKind.ErrorReturned],
                InputTokens = _inputTokens,
                CachedInputTokens = _cachedInputTokens,
                CacheCreationInputTokens = _cacheCreationInputTokens,
                OutputTokens = _outputTokens,
                ReasoningTokens = _reasoningTokens,
                TotalTokens = _totalTokens,
                Events = [.. _events],
            };
        }
    }

    private static bool HasRequiredToken(string? required, string? supplied)
    {
        return string.IsNullOrEmpty(required) || string.Equals(required, supplied, StringComparison.Ordinal);
    }

    private static IEnumerable<LlmTckScenario> GetChatScenarios(LlmTckConfiguration configuration)
    {
        return configuration.ChatScenarios.Concat(
            configuration.Datasets.SelectMany(dataset => dataset.ChatScenarios)
        );
    }

    private bool IsConfiguredModel(string modelId, LlmTckModelKind kind)
    {
        return _configuration.Models.Any(model =>
            model.Kind == kind && string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)
        );
    }

    private int GetReasoningTokens(string modelId)
    {
        return _configuration
            .Models
            .FirstOrDefault(model =>
                model.Kind == LlmTckModelKind.Chat
                && string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)
            )
            ?.ReasoningTokens ?? 0;
    }

    private LlmTckRuntimeFault? TryCreateFault(
        string modelId,
        IEnumerable<string?> requestContent,
        string? request = null,
        LlmTckTokenUsage? usage = null,
        LlmTckChatRequest? chatRequest = null
    )
    {
        var faultSimulation = _configuration.FaultSimulation;

        if (
            faultSimulation.MaxRequestsBeforeRateLimit is int allowedRequests
            && allowedRequests >= 0
            && _requestCount >= allowedRequests
        )
        {
            var message = $"{_tooManyRequestsCode}: configured LLM TCK request limit was exceeded.";
            AddEvent(
                LlmTckEventKind.ErrorReturned,
                null,
                modelId,
                message,
                request,
                message,
                usage,
                chatRequest: chatRequest
            );
            return new LlmTckRuntimeFault(429, _tooManyRequestsCode, message, usage);
        }

        _requestCount++;

        var filteredTerm = FindContentFilterTerm(
            requestContent,
            faultSimulation.ContentFilterTerms
        );
        if (filteredTerm is null)
        {
            return null;
        }

        var filterMessage =
            $"{_contentFilterCode}: request matched configured LLM TCK content filter term '{filteredTerm}'.";
        AddEvent(
            LlmTckEventKind.ErrorReturned,
            null,
            modelId,
            filterMessage,
            request,
            filterMessage,
            usage,
            chatRequest: chatRequest
        );
        return new LlmTckRuntimeFault(400, _contentFilterCode, filterMessage, usage);
    }

    private static string? FindContentFilterTerm(
        IEnumerable<string?> requestContent,
        IReadOnlyList<string> contentFilterTerms
    )
    {
        if (contentFilterTerms.Count == 0)
        {
            return null;
        }

        foreach (var value in requestContent)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var matchedTerm = contentFilterTerms.FirstOrDefault(term =>
                value.Contains(term, StringComparison.OrdinalIgnoreCase)
            );
            if (matchedTerm is not null)
            {
                return matchedTerm;
            }
        }

        return null;
    }

    private static LlmTckChatResult UnknownModel(
        string modelId,
        LlmTckModelKind kind,
        LlmTckTokenUsage? usage = null
    )
    {
        return LlmTckChatResult.Failure(
            modelId,
            404,
            "llm_tck_unknown_model",
            CreateUnknownModelMessage(modelId, kind),
            usage: usage
        );
    }

    private static LlmTckEmbeddingResult UnknownEmbeddingModel(string modelId)
    {
        return new()
        {
            IsSuccess = false,
            StatusCode = 404,
            ModelId = modelId,
            ErrorCode = "llm_tck_unknown_model",
            ErrorMessage = CreateUnknownModelMessage(modelId, LlmTckModelKind.Embedding),
        };
    }

    private static LlmTckImageResult UnknownImageModel(string modelId)
    {
        return new()
        {
            IsSuccess = false,
            StatusCode = 404,
            ModelId = modelId,
            ErrorCode = "llm_tck_unknown_model",
            ErrorMessage = CreateUnknownModelMessage(modelId, LlmTckModelKind.Image),
        };
    }

    private static LlmTckAudioResult UnknownAudioModel(string modelId)
    {
        return new()
        {
            IsSuccess = false,
            StatusCode = 404,
            ModelId = modelId,
            ErrorCode = "llm_tck_unknown_model",
            ErrorMessage = CreateUnknownModelMessage(modelId, LlmTckModelKind.Audio),
        };
    }

    private static LlmTckTranscriptionResult UnknownTranscriptionModel(string modelId)
    {
        return new()
        {
            IsSuccess = false,
            StatusCode = 404,
            ModelId = modelId,
            ErrorCode = "llm_tck_unknown_model",
            ErrorMessage = CreateUnknownModelMessage(modelId, LlmTckModelKind.Audio),
        };
    }

    private static LlmTckVideoResult UnknownVideoModel(
        string modelId,
        LlmTckTokenUsage? usage = null
    )
    {
        return new()
        {
            IsSuccess = false,
            StatusCode = 404,
            ModelId = modelId,
            ErrorCode = "llm_tck_unknown_model",
            ErrorMessage = CreateUnknownModelMessage(modelId, LlmTckModelKind.Video),
            Usage = usage ?? new LlmTckTokenUsage(),
        };
    }

    private static string CreateUnknownModelMessage(string modelId, LlmTckModelKind kind)
    {
        return $"Model '{modelId}' is not configured for {kind.ToString().ToLowerInvariant()} requests.";
    }

    private void AddModelNotFoundEvent(
        string modelId,
        LlmTckModelKind kind,
        string? request = null,
        LlmTckTokenUsage? usage = null,
        LlmTckChatRequest? chatRequest = null
    )
    {
        AddEvent(
            LlmTckEventKind.ModelNotFound,
            null,
            modelId,
            CreateUnknownModelMessage(modelId, kind),
            request,
            usage: usage,
            chatRequest: chatRequest
        );
    }

    private void RollBackReservedResponse(
        string scenarioId,
        int reservedPosition,
        long generation
    )
    {
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            if (_scenarioPositions.GetValueOrDefault(scenarioId) == reservedPosition + 1)
            {
                _scenarioPositions[scenarioId] = reservedPosition;
            }
        }
    }

    private LlmTckChatResult CreateSupersededChatResult(
        LlmTckChatRequest request,
        string? scenarioId
    )
    {
        return LlmTckChatResult.Failure(
            request.ModelId,
            409,
            _requestSupersededCode,
            _requestSupersededMessage,
            scenarioId,
            CreateChatUsage(request)
        );
    }

    private bool IsActiveRequestSuperseded()
    {
        return _activeRequest.Value is { } request && request.Generation != _generation;
    }

    private static LlmTckEmbeddingResult CreateSupersededEmbeddingResult(string modelId)
    {
        return new LlmTckEmbeddingResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ModelId = modelId,
            ErrorCode = _requestSupersededCode,
            ErrorMessage = _requestSupersededMessage,
        };
    }

    private static LlmTckImageResult CreateSupersededImageResult(string modelId)
    {
        return new LlmTckImageResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ModelId = modelId,
            ErrorCode = _requestSupersededCode,
            ErrorMessage = _requestSupersededMessage,
        };
    }

    private static LlmTckAudioResult CreateSupersededAudioResult(string modelId)
    {
        return new LlmTckAudioResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ModelId = modelId,
            ErrorCode = _requestSupersededCode,
            ErrorMessage = _requestSupersededMessage,
        };
    }

    private static LlmTckTranscriptionResult CreateSupersededTranscriptionResult(
        string modelId
    )
    {
        return new LlmTckTranscriptionResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ModelId = modelId,
            ErrorCode = _requestSupersededCode,
            ErrorMessage = _requestSupersededMessage,
        };
    }

    private static LlmTckVideoResult CreateSupersededVideoResult(string modelId)
    {
        return new LlmTckVideoResult
        {
            IsSuccess = false,
            StatusCode = 409,
            ModelId = modelId,
            ErrorCode = _requestSupersededCode,
            ErrorMessage = _requestSupersededMessage,
        };
    }

    private static bool ScenarioMatches(LlmTckScenario scenario, LlmTckChatRequest request)
    {
        if (!string.Equals(scenario.ModelId, request.ModelId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return scenario.Match.Messages.Count == 0 || scenario.Match.Mode switch
        {
            LlmTckMatchMode.Exact => ExactMessagesMatch(scenario.Match.Messages, request.Messages),
            _ => ContainsMessagesMatch(scenario.Match.Messages, request.Messages),
        };
    }

    private static bool ExactMessagesMatch(
        IReadOnlyList<LlmTckMessage> expected,
        IReadOnlyList<LlmTckMessage> actual
    )
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (!MessageMatches(expected[i], actual[i], LlmTckMatchMode.Exact))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsMessagesMatch(
        IReadOnlyList<LlmTckMessage> expected,
        IReadOnlyList<LlmTckMessage> actual
    )
    {
        return expected.All(item =>
            actual.Any(candidate => MessageMatches(item, candidate, LlmTckMatchMode.Contains))
        );
    }

    private static bool MessageMatches(
        LlmTckMessage expected,
        LlmTckMessage actual,
        LlmTckMatchMode mode
    )
    {
        var roleMatches = string.IsNullOrWhiteSpace(expected.Role)
            || string.Equals(expected.Role, actual.Role, StringComparison.OrdinalIgnoreCase);
        var contentMatches = mode == LlmTckMatchMode.Exact
            ? string.Equals(expected.Content, actual.Content, StringComparison.Ordinal)
            : actual.Content.Contains(expected.Content, StringComparison.OrdinalIgnoreCase);

        return roleMatches && contentMatches;
    }

    private void AddEvent(
        LlmTckEventKind kind,
        string? scenarioId,
        string modelId,
        string message,
        string? request = null,
        string? response = null,
        LlmTckTokenUsage? usage = null,
        LlmTckChatRequest? chatRequest = null,
        IReadOnlyList<string>? streamChunks = null
    )
    {
        var activeRequest = _activeRequest.Value;
        if (activeRequest is { } && activeRequest.Value.Generation != _generation)
        {
            return;
        }

        var runtimeEvent = BoundEventPayload(new LlmTckRuntimeEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Kind = kind,
            RequestId = activeRequest?.RequestId ?? chatRequest?.RequestId,
            ScenarioId = scenarioId,
            ModelId = modelId,
            Message = message,
            Request = request,
            Messages = chatRequest?.Messages ?? [],
            Response = response,
            IsStreaming = chatRequest?.Stream ?? false,
            StreamChunks = streamChunks ?? [],
            PromptCachePolicy = chatRequest?.PromptCachePolicy ?? LlmTckPromptCachePolicy.None,
            PromptCacheKey = chatRequest?.PromptCacheKey,
            Usage = usage,
        });
        RecordEventTotals(runtimeEvent);
        var retainedCharacters = CountRetainedCharacters(runtimeEvent);
        while (
            _events.Count >= _maxRetainedEvents
            || (_events.Count > 0
                && _retainedEventCharacters + retainedCharacters > _maxRetainedEventCharacters)
        )
        {
            RemoveOldestEvent();
        }

        _events.Enqueue(runtimeEvent);
        _retainedEventCharacters += retainedCharacters;
        if (!string.IsNullOrWhiteSpace(runtimeEvent.RequestId))
        {
            _eventsByRequestId[runtimeEvent.RequestId] = runtimeEvent;
        }
    }

    private void ResetEventJournal()
    {
        _events.Clear();
        _eventsByRequestId.Clear();
        Array.Clear(_eventKindCounts);
        _totalEvents = 0;
        _inputTokens = 0;
        _cachedInputTokens = 0;
        _cacheCreationInputTokens = 0;
        _outputTokens = 0;
        _reasoningTokens = 0;
        _totalTokens = 0;
        _retainedEventCharacters = 0;
    }

    private void RemoveOldestEvent()
    {
        var oldest = _events.Dequeue();
        _retainedEventCharacters -= CountRetainedCharacters(oldest);
        if (!string.IsNullOrWhiteSpace(oldest.RequestId)
            && _eventsByRequestId.TryGetValue(oldest.RequestId, out var indexed)
            && ReferenceEquals(indexed, oldest))
        {
            _eventsByRequestId.Remove(oldest.RequestId);
        }
    }

    private static LlmTckRuntimeEvent BoundEventPayload(LlmTckRuntimeEvent runtimeEvent)
    {
        var truncated = false;
        var requestId = TruncateMetadata(runtimeEvent.RequestId, ref truncated);
        var scenarioId = TruncateMetadata(runtimeEvent.ScenarioId, ref truncated);
        var modelId = TruncateMetadata(runtimeEvent.ModelId, ref truncated) ?? string.Empty;
        var message = TruncateMetadata(runtimeEvent.Message, ref truncated) ?? string.Empty;
        var promptCacheKey = TruncateMetadata(runtimeEvent.PromptCacheKey, ref truncated);
        var remaining = Math.Max(
            0,
            _maxRetainedEventCharacters
                - CountCharacters(requestId)
                - CountCharacters(scenarioId)
                - modelId.Length
                - message.Length
                - CountCharacters(promptCacheKey)
        );
        var response = RetainText(runtimeEvent.Response, ref remaining, ref truncated);
        var streamChunks = RetainChunks(runtimeEvent.StreamChunks, ref remaining, ref truncated);
        var messages = RetainMessages(runtimeEvent.Messages, ref remaining, ref truncated);
        var request = RetainText(runtimeEvent.Request, ref remaining, ref truncated);

        return runtimeEvent with
        {
            RequestId = requestId,
            ScenarioId = scenarioId,
            ModelId = modelId,
            Message = message,
            PromptCacheKey = promptCacheKey,
            Messages = messages,
            Response = response,
            StreamChunks = streamChunks,
            Request = request,
            PayloadTruncated = truncated,
        };
    }

    private static IReadOnlyList<LlmTckMessage> RetainMessages(
        IReadOnlyList<LlmTckMessage> messages,
        ref int remaining,
        ref bool truncated
    )
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var maximumItemCount = remaining / _retainedMessageCost;
        var retained = new List<LlmTckMessage>(Math.Min(messages.Count, maximumItemCount));
        foreach (var source in messages)
        {
            if (remaining < _retainedMessageCost)
            {
                truncated = true;
                break;
            }

            remaining -= _retainedMessageCost;
            var role = RetainRequiredText(source.Role, ref remaining, ref truncated);
            var content = RetainRequiredText(source.Content, ref remaining, ref truncated);
            retained.Add(new LlmTckMessage { Role = role, Content = content });
        }

        return retained.Count == 0 ? [] : Array.AsReadOnly(retained.ToArray());
    }

    private static IReadOnlyList<string> RetainChunks(
        IReadOnlyList<string> chunks,
        ref int remaining,
        ref bool truncated
    )
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        var maximumItemCount = remaining / _retainedChunkCost;
        var retained = new List<string>(Math.Min(chunks.Count, maximumItemCount));
        foreach (var chunk in chunks)
        {
            if (remaining < _retainedChunkCost)
            {
                truncated = true;
                break;
            }

            remaining -= _retainedChunkCost;
            retained.Add(RetainRequiredText(chunk, ref remaining, ref truncated));
        }

        return retained.Count == 0 ? [] : Array.AsReadOnly(retained.ToArray());
    }

    private static string? TruncateMetadata(string? value, ref bool truncated)
    {
        if (value is null || value.Length <= _maxRetainedMetadataCharacters)
        {
            return value;
        }

        truncated = true;
        return value[.._maxRetainedMetadataCharacters];
    }

    private static string? RetainText(string? value, ref int remaining, ref bool truncated)
    {
        if (value is null)
        {
            return null;
        }

        return RetainRequiredText(value, ref remaining, ref truncated);
    }

    private static string RetainRequiredText(
        string value,
        ref int remaining,
        ref bool truncated
    )
    {
        if (value.Length <= remaining)
        {
            remaining -= value.Length;
            return value;
        }

        truncated = true;
        var retained = remaining == 0 ? string.Empty : value[..remaining];
        remaining = 0;
        return retained;
    }

    private static int CountRetainedCharacters(LlmTckRuntimeEvent runtimeEvent)
    {
        var total = CountCharacters(runtimeEvent.RequestId)
            + CountCharacters(runtimeEvent.ScenarioId)
            + runtimeEvent.ModelId.Length
            + runtimeEvent.Message.Length
            + CountCharacters(runtimeEvent.Request)
            + CountCharacters(runtimeEvent.Response)
            + CountCharacters(runtimeEvent.PromptCacheKey);
        foreach (var item in runtimeEvent.Messages)
        {
            total += _retainedMessageCost + item.Role.Length + item.Content.Length;
        }

        foreach (var item in runtimeEvent.StreamChunks)
        {
            total += _retainedChunkCost + item.Length;
        }

        return total;
    }

    private static int CountCharacters(string? value)
    {
        return value?.Length ?? 0;
    }

    private void RecordEventTotals(LlmTckRuntimeEvent runtimeEvent)
    {
        _totalEvents++;
        _eventKindCounts[(int)runtimeEvent.Kind]++;
        if (runtimeEvent.Usage is not { } usage)
        {
            return;
        }

        _inputTokens += usage.InputTokens;
        _cachedInputTokens += usage.CachedInputTokens;
        _cacheCreationInputTokens += usage.CacheCreationInputTokens;
        _outputTokens += usage.OutputTokens;
        _reasoningTokens += usage.ReasoningTokens;
        _totalTokens += usage.TotalTokens;
    }

    private LlmTckTokenUsage CreateChatUsage(
        LlmTckChatRequest request,
        string? response = null,
        bool updatePromptCache = false
    )
    {
        var inputTokens = request.Messages.Sum(message =>
            LlmTckTokenCounter.CountTextTokens(message.Content)
        );
        var (cachedInputTokens, cacheCreationInputTokens) = CalculatePromptCacheUsage(
            request,
            updatePromptCache
        );
        var visibleOutputTokens = LlmTckTokenCounter.CountTextTokens(response);
        var reasoningTokens = GetReasoningTokens(request.ModelId);
        var outputTokens = visibleOutputTokens + reasoningTokens;
        return new LlmTckTokenUsage
        {
            InputTokens = inputTokens,
            CachedInputTokens = cachedInputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            TotalTokens = inputTokens + outputTokens,
        };
    }

    private (int CachedInputTokens, int CacheCreationInputTokens) CalculatePromptCacheUsage(
        LlmTckChatRequest request,
        bool updatePromptCache
    )
    {
        if (!updatePromptCache || request.PromptCachePolicy == LlmTckPromptCachePolicy.None)
        {
            return (0, 0);
        }

        var candidates = CreatePromptCacheCandidates(request);
        if (candidates.Count == 0)
        {
            return (0, 0);
        }

        var cachedInputTokens = 0;
        var maxCacheableTokens = 0;

        foreach (var candidate in candidates)
        {
            if (_promptCacheEntries.TryGetValue(candidate.Key, out var cachedTokens))
            {
                cachedInputTokens = Math.Max(cachedInputTokens, cachedTokens);
            }

            maxCacheableTokens = Math.Max(maxCacheableTokens, candidate.Tokens);
            _promptCacheEntries[candidate.Key] = candidate.Tokens;
        }

        return (cachedInputTokens, Math.Max(0, maxCacheableTokens - cachedInputTokens));
    }

    private static List<PromptCacheCandidate> CreatePromptCacheCandidates(
        LlmTckChatRequest request
    )
    {
        var candidates = new List<PromptCacheCandidate>();
        var cachePrefix = new StringBuilder();
        var cumulativeTokens = 0;

        foreach (var message in request.Messages)
        {
            cumulativeTokens += LlmTckTokenCounter.CountTextTokens(message.Content);
            cachePrefix
                .Append("role:")
                .Append(message.Role)
                .Append('\u001f')
                .Append("content:")
                .Append(message.Content)
                .Append('\u001e');

            var cacheableTokens = RoundPromptCacheTokens(
                request.PromptCachePolicy,
                cumulativeTokens
            );
            if (cacheableTokens <= 0)
            {
                continue;
            }

            candidates.Add(
                new PromptCacheCandidate(
                    CreatePromptCacheEntryKey(request, cachePrefix.ToString(), cacheableTokens),
                    cacheableTokens
                )
            );
        }

        return candidates;
    }

    private static int RoundPromptCacheTokens(
        LlmTckPromptCachePolicy policy,
        int tokenCount
    )
    {
        var minimumTokens = GetPromptCacheMinimumTokens(policy);
        if (tokenCount < minimumTokens)
        {
            return 0;
        }

        var incrementTokens = GetPromptCacheIncrementTokens(policy);
        return minimumTokens + ((tokenCount - minimumTokens) / incrementTokens * incrementTokens);
    }

    private static int GetPromptCacheMinimumTokens(LlmTckPromptCachePolicy policy)
    {
        return policy switch
        {
            LlmTckPromptCachePolicy.Mistral => _mistralPromptCacheMinimumTokens,
            LlmTckPromptCachePolicy.Gemini => _geminiPromptCacheMinimumTokens,
            _ => _openAiPromptCacheMinimumTokens,
        };
    }

    private static int GetPromptCacheIncrementTokens(LlmTckPromptCachePolicy policy)
    {
        return policy switch
        {
            LlmTckPromptCachePolicy.Mistral => _mistralPromptCacheIncrementTokens,
            LlmTckPromptCachePolicy.Gemini => _geminiPromptCacheIncrementTokens,
            _ => _openAiPromptCacheIncrementTokens,
        };
    }

    private static string CreatePromptCacheEntryKey(
        LlmTckChatRequest request,
        string prefix,
        int cacheableTokens
    )
    {
        var keyMaterial = string.Join(
            '\u001d',
            request.PromptCachePolicy.ToString(),
            request.ModelId,
            request.PromptCacheKey ?? string.Empty,
            cacheableTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
            prefix
        );
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)));
    }

    private readonly record struct PromptCacheCandidate(string Key, int Tokens);

    private readonly record struct RuntimeRequestContext(string RequestId, long Generation);

    private static LlmTckTokenUsage CreateVideoUsage(string prompt)
    {
        var inputTokens = LlmTckTokenCounter.CountTextTokens(prompt);
        return new LlmTckTokenUsage
        {
            InputTokens = inputTokens,
            TotalTokens = inputTokens,
        };
    }

    private static string FormatChatRequest(LlmTckChatRequest request)
    {
        if (request.Messages.Count == 0)
        {
            return "(no messages)";
        }

        var builder = new StringBuilder();
        foreach (var message in request.Messages)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(message.Role).Append(": ").Append(Truncate(message.Content, 800));
        }

        return builder.ToString();
    }

    private static string FormatAudioRequest(string fileName, string? prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? $"file: {fileName}"
            : $"file: {fileName}\nprompt: {Truncate(prompt, 800)}";
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }

    private sealed record LlmTckRuntimeFault(
        int StatusCode,
        string Code,
        string Message,
        LlmTckTokenUsage? Usage = null
    );

    private sealed class RuntimeRequestScope(
        AsyncLocal<RuntimeRequestContext?> activeRequest,
        RuntimeRequestContext? previousRequest
    ) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            activeRequest.Value = previousRequest;
            _disposed = true;
        }
    }
}

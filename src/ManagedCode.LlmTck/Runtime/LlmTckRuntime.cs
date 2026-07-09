using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed class LlmTckRuntime : ILlmTckRuntime
{
    private const string _tooManyRequestsCode = "too_many_requests";
    private const string _contentFilterCode = "content_filter";

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _scenarioPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LlmTckRuntimeEvent> _events = [];
    private LlmTckConfiguration _configuration = LlmTckConfiguration.CreateDefault();
    private int _requestCount;

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
            _events.Clear();
            _requestCount = 0;
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _scenarioPositions.Clear();
            _events.Clear();
            _requestCount = 0;
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

        lock (_gate)
        {
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                var usage = CreateChatUsage(request);
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    request.ModelId,
                    "Global bearer token requirement failed.",
                    FormatChatRequest(request),
                    usage: usage
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
                    usage
                );
                return UnknownModel(request.ModelId, LlmTckModelKind.Chat, usage);
            }

            var fault = TryCreateFault(
                request.ModelId,
                request.Messages.Select(message => message.Content),
                FormatChatRequest(request),
                CreateChatUsage(request)
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
                    usage: usage
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
                    usage: usage
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
                    usage: usage
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
            RollBackReservedResponse(scenario.Id, responsePosition);
            throw;
        }

        lock (_gate)
        {
            var responseText = response.Error?.Message ?? response.Content;
            var usage = CreateChatUsage(request, responseText);
            AddEvent(
                response.Error is null ? LlmTckEventKind.Matched : LlmTckEventKind.ErrorReturned,
                scenario.Id,
                request.ModelId,
                response.Error?.Message ?? "Scenario matched.",
                FormatChatRequest(request),
                responseText,
                usage
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
                response.StreamChunks.Count > 0 ? response.StreamChunks : [response.Content],
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
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Embedding);
                return Task.FromResult(UnknownEmbeddingModel(modelId));
            }

            var fault = TryCreateFault(modelId, inputs, request: string.Join("\n", inputs));
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
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(LlmTckEventKind.AuthFailed, null, modelId, "Image auth failed.");
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

            var fault = TryCreateFault(modelId, [prompt], request: prompt);
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

            AddEvent(LlmTckEventKind.Matched, null, modelId, $"Generated image for '{prompt}'.");
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
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(LlmTckEventKind.AuthFailed, null, modelId, "Audio auth failed.");
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

            var fault = TryCreateFault(modelId, [input], request: input);
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

            AddEvent(LlmTckEventKind.Matched, null, modelId, $"Generated audio for '{input}'.");
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
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(LlmTckEventKind.AuthFailed, null, modelId, "Audio transcription auth failed.");
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
            if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
            {
                AddEvent(LlmTckEventKind.AuthFailed, null, modelId, "Audio translation auth failed.");
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
                AddModelNotFoundEvent(modelId, LlmTckModelKind.Video, usage: usage);
                return Task.FromResult(UnknownVideoModel(modelId, usage));
            }

            var fault = TryCreateFault(modelId, [prompt], request: prompt, usage: usage);
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
                TotalEvents = _events.Count,
                Matched = _events.Count(item => item.Kind == LlmTckEventKind.Matched),
                Unmatched = _events.Count(item => item.Kind == LlmTckEventKind.Unmatched),
                ModelNotFound = _events.Count(item => item.Kind == LlmTckEventKind.ModelNotFound),
                AuthFailed = _events.Count(item => item.Kind == LlmTckEventKind.AuthFailed),
                ScenarioExhausted = _events.Count(item => item.Kind == LlmTckEventKind.ScenarioExhausted),
                ErrorsReturned = _events.Count(item => item.Kind == LlmTckEventKind.ErrorReturned),
                InputTokens = _events.Sum(item => item.Usage?.InputTokens ?? 0),
                OutputTokens = _events.Sum(item => item.Usage?.OutputTokens ?? 0),
                ReasoningTokens = _events.Sum(item => item.Usage?.ReasoningTokens ?? 0),
                TotalTokens = _events.Sum(item => item.Usage?.TotalTokens ?? 0),
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
        LlmTckTokenUsage? usage = null
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
                usage
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
            usage
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
        LlmTckTokenUsage? usage = null
    )
    {
        AddEvent(
            LlmTckEventKind.ModelNotFound,
            null,
            modelId,
            CreateUnknownModelMessage(modelId, kind),
            request,
            usage: usage
        );
    }

    private void RollBackReservedResponse(string scenarioId, int reservedPosition)
    {
        lock (_gate)
        {
            if (_scenarioPositions.GetValueOrDefault(scenarioId) == reservedPosition + 1)
            {
                _scenarioPositions[scenarioId] = reservedPosition;
            }
        }
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
        LlmTckTokenUsage? usage = null
    )
    {
        _events.Add(
            new LlmTckRuntimeEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Kind = kind,
                ScenarioId = scenarioId,
                ModelId = modelId,
                Message = message,
                Request = request,
                Response = response,
                Usage = usage,
            }
        );
    }

    private LlmTckTokenUsage CreateChatUsage(
        LlmTckChatRequest request,
        string? response = null
    )
    {
        var inputTokens = request.Messages.Sum(message =>
            LlmTckTokenCounter.CountTextTokens(message.Content)
        );
        var visibleOutputTokens = LlmTckTokenCounter.CountTextTokens(response);
        var reasoningTokens = GetReasoningTokens(request.ModelId);
        var outputTokens = visibleOutputTokens + reasoningTokens;
        return new LlmTckTokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            TotalTokens = inputTokens + outputTokens,
        };
    }

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

        return string.Join(
            "\n",
            request.Messages.Select(message => $"{message.Role}: {Truncate(message.Content, 800)}")
        );
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
}

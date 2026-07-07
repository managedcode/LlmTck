using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed class LlmTckRuntime : ILlmTckRuntime
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _scenarioPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LlmTckRuntimeEvent> _events = [];
    private LlmTckConfiguration _configuration = LlmTckConfiguration.CreateDefault();

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
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    null,
                    request.ModelId,
                    "Global bearer token requirement failed."
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    401,
                    "invalid_api_key",
                    "The supplied bearer token did not match the configured LLM TCK token."
                );
            }

            if (!IsConfiguredModel(request.ModelId, LlmTckModelKind.Chat))
            {
                AddModelNotFoundEvent(request.ModelId, LlmTckModelKind.Chat);
                return UnknownModel(request.ModelId, LlmTckModelKind.Chat);
            }

            scenario = GetChatScenarios(_configuration)
                .FirstOrDefault(candidate => ScenarioMatches(candidate, request));

            if (scenario is null)
            {
                AddEvent(
                    LlmTckEventKind.Unmatched,
                    null,
                    request.ModelId,
                    "No configured scenario matched the request."
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    404,
                    "llm_tck_unmatched_request",
                    "No configured LLM TCK scenario matched the request."
                );
            }

            if (!HasRequiredToken(scenario.RequiredBearerToken, bearerToken))
            {
                AddEvent(
                    LlmTckEventKind.AuthFailed,
                    scenario.Id,
                    request.ModelId,
                    "Scenario bearer token requirement failed."
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    401,
                    "invalid_api_key",
                    "The supplied bearer token did not match the scenario token.",
                    scenario.Id
                );
            }

            responsePosition = _scenarioPositions.GetValueOrDefault(scenario.Id);
            if (responsePosition >= scenario.Responses.Count)
            {
                AddEvent(
                    LlmTckEventKind.ScenarioExhausted,
                    scenario.Id,
                    request.ModelId,
                    "Scenario response queue is exhausted."
                );
                return LlmTckChatResult.Failure(
                    request.ModelId,
                    409,
                    "llm_tck_scenario_exhausted",
                    "The matched scenario has no responses left.",
                    scenario.Id
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
            AddEvent(
                response.Error is null ? LlmTckEventKind.Matched : LlmTckEventKind.ErrorReturned,
                scenario.Id,
                request.ModelId,
                response.Error?.Message ?? "Scenario matched."
            );
        }

        if (response.Error is not null)
        {
            return LlmTckChatResult.Failure(
                request.ModelId,
                response.Error.StatusCode,
                response.Error.Code,
                response.Error.Message,
                scenario.Id
            );
        }

        return LlmTckChatResult.Success(
            request.ModelId,
            scenario.Id,
            response.Content,
            response.StreamChunks.Count > 0 ? response.StreamChunks : [response.Content]
        );
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

    private static LlmTckChatResult UnknownModel(string modelId, LlmTckModelKind kind)
    {
        return LlmTckChatResult.Failure(
            modelId,
            404,
            "llm_tck_unknown_model",
            CreateUnknownModelMessage(modelId, kind)
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

    private static string CreateUnknownModelMessage(string modelId, LlmTckModelKind kind)
    {
        return $"Model '{modelId}' is not configured for {kind.ToString().ToLowerInvariant()} requests.";
    }

    private void AddModelNotFoundEvent(string modelId, LlmTckModelKind kind)
    {
        AddEvent(
            LlmTckEventKind.ModelNotFound,
            null,
            modelId,
            CreateUnknownModelMessage(modelId, kind)
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
        string message
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
            }
        );
    }
}

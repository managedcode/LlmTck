using System.Globalization;
using System.Text.Json;
using ManagedCode.LlmTck.Decisions;
using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.Runtime;

public sealed partial class LlmTckRuntime
{
    public Task<LlmTckDecisionResult> DecideAsync(LlmTckDecisionRequest request,
        string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);
        ArgumentNullException.ThrowIfNull(request.Questions);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(DecideCore(request, bearerToken));
        }
    }

    private LlmTckDecisionResult DecideCore(LlmTckDecisionRequest request, string? bearerToken)
    {
        if (IsActiveRequestSuperseded())
        {
            return DecisionFailure(request, 409, _requestSupersededCode, _requestSupersededMessage);
        }
        if (!HasRequiredToken(_configuration.RequiredBearerToken, bearerToken))
        {
            return DecisionFailure(request, 401, "invalid_api_key", "The bearer token did not match.", kind: LlmTckEventKind.AuthFailed);
        }
        if (!IsConfiguredModel(request.ModelId, LlmTckModelKind.Decision))
        {
            return DecisionFailure(request, 404, "model_not_found", "The decision model is not configured.", kind: LlmTckEventKind.ModelNotFound);
        }
        var fault = TryCreateFault(request.ModelId, [request.Input], JsonSerializer.Serialize(request));
        if (fault is not null)
        {
            return new() { ModelId = request.ModelId, StatusCode = fault.StatusCode, ErrorCode = fault.Code, ErrorMessage = fault.Message };
        }
        var scenario = _configuration.DecisionScenarios.Find(candidate =>
            candidate.ModelId == request.ModelId && (candidate.ExpectedInput is null || candidate.ExpectedInput == request.Input));
        if (scenario is null)
        {
            return DecisionFailure(request, 404, "llm_tck_scenario_not_found", "No decision scenario matched.", kind: LlmTckEventKind.Unmatched);
        }
        if (scenario.Error is { } error)
        {
            return DecisionFailure(request, error.StatusCode, error.Code, error.Message, scenario.Id);
        }
        if (!DecisionAnswersMatch(request, scenario))
        {
            return DecisionFailure(request, 409, "llm_tck_fixture_mismatch", "Configured decision answers do not match the requested questions.", scenario.Id);
        }
        var answers = scenario.Answers.ToDictionary(entry => entry.Key,
            entry => entry.Value with { Probabilities = new(entry.Value.Probabilities) }, StringComparer.Ordinal);
        AddEvent(LlmTckEventKind.Matched, scenario.Id, request.ModelId, "Decision scenario matched.",
            JsonSerializer.Serialize(request), JsonSerializer.Serialize(answers), scenario.Usage);
        return new() { IsSuccess = true, ModelId = request.ModelId, ScenarioId = scenario.Id, Answers = answers, Usage = scenario.Usage };
    }

    private static bool DecisionAnswersMatch(LlmTckDecisionRequest request, LlmTckDecisionScenario scenario)
    {
        return request.Questions.Count == scenario.Answers.Count
            && request.Questions.Select(question => question.Name).Distinct(StringComparer.Ordinal).Count() == request.Questions.Count
            && request.Questions.All(question => scenario.Answers.TryGetValue(question.Name, out var answer)
                && answer.Kind == question.Kind && (answer.Refused || DecisionAnswerFitsQuestion(question, answer)));
    }

    private static bool DecisionAnswerFitsQuestion(LlmTckDecisionQuestion question, LlmTckDecisionAnswer answer)
    {
        if (question.Kind == LlmTckDecisionKind.Choice)
        { return question.Options.Contains(answer.Choice!, StringComparer.Ordinal) && answer.Probabilities.Keys.All(key => question.Options.Contains(key, StringComparer.Ordinal)); }
        if (question.Kind == LlmTckDecisionKind.Score)
        { return answer.Score <= question.Options.Count - 1 && answer.Probabilities.Keys.All(key => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < question.Options.Count); }
        return true;
    }

    private LlmTckDecisionResult DecisionFailure(LlmTckDecisionRequest request, int status, string code,
        string message, string? scenario = null, LlmTckEventKind kind = LlmTckEventKind.ErrorReturned)
    {
        AddEvent(kind, scenario, request.ModelId, message, JsonSerializer.Serialize(request), message);
        return new() { ModelId = request.ModelId, ScenarioId = scenario, StatusCode = status, ErrorCode = code, ErrorMessage = message };
    }
}

using System.Globalization;
using System.Text.Json;
using ManagedCode.LlmTck.Decisions;
using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.SystemOne;

public static class SystemOneDecisionMapper
{
    private const int _nativeProbabilityPrecision = 4;
    public const int MaxChoices = 255;
    public const int JevMaxScoreLevels = 10;
    public const int KevMaxScoreLevels = 255;
    public static LlmTckRequestValidationResult Validate(JsonElement body)
    {
        return Validate(body, MaxChoices, KevMaxScoreLevels);
    }

    public static LlmTckRequestValidationResult Validate(JsonElement body, int maxChoices, int maxScoreLevels)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("model", out var model)
            || model.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(model.GetString())
            || !body.TryGetProperty("state", out var state) || !IsContent(state) || state.ValueKind == JsonValueKind.Null
            || !body.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Object)
        { return new("model, string/object/array state and object questions are required."); }
        var entries = questions.EnumerateObject().ToArray();
        if (entries.Length == 0 || entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        { return new("Questions must have unique nonempty names."); }
        return entries.All(entry => !string.IsNullOrWhiteSpace(entry.Name) && ValidQuestion(entry.Value, maxChoices, maxScoreLevels))
            ? new() : new("Every question requires a supported type, instructions and matching criteria.");
    }

    private static bool ValidQuestion(JsonElement question, int maxChoices, int maxScoreLevels)
    {
        if (question.ValueKind != JsonValueKind.Object || !question.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String) { return false; }
        if (question.TryGetProperty("instructions", out var instructions) && !IsContent(instructions)) { return false; }
        var hasCriteria = question.TryGetProperty("criteria", out var criteria);
        if (type.GetString() == SystemOneDecisionTypes.Predicate)
        {
            return !hasCriteria || criteria.ValueKind == JsonValueKind.Null || (criteria.ValueKind == JsonValueKind.Object
                && criteria.EnumerateObject().All(entry => entry.Name is "true" or "false" && IsContent(entry.Value)));
        }
        if (!hasCriteria) { return false; }
        if (type.GetString() == SystemOneDecisionTypes.Score)
        { return criteria.ValueKind == JsonValueKind.Array && criteria.GetArrayLength() >= 1 && criteria.GetArrayLength() <= maxScoreLevels && criteria.EnumerateArray().All(value => IsContent(value) && value.ValueKind != JsonValueKind.Null); }
        if (type.GetString() != SystemOneDecisionTypes.Choice || criteria.ValueKind != JsonValueKind.Object) { return false; }
        var entries = criteria.EnumerateObject().ToArray();
        return entries.Length >= 1 && entries.Length <= maxChoices && entries.All(entry => !string.IsNullOrWhiteSpace(entry.Name) && IsContent(entry.Value))
            && entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() == entries.Length;
    }

    private static bool IsContent(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null;
    }

    public static LlmTckDecisionRequest ToRequest(SystemOneDecisionRequest request)
    {
        return new()
        {
            ModelId = request.Model,
            Input = JsonSerializer.Serialize(request.State),
            Questions = request.Questions.Select(entry => new LlmTckDecisionQuestion
            {
                Name = entry.Key,
                Kind = entry.Value.Type switch
                { SystemOneDecisionTypes.Predicate => LlmTckDecisionKind.Predicate, SystemOneDecisionTypes.Choice => LlmTckDecisionKind.Choice, _ => LlmTckDecisionKind.Score },
                Options = entry.Value.Type == SystemOneDecisionTypes.Predicate ? [] : entry.Value.Type == SystemOneDecisionTypes.Score
                    ? entry.Value.Criteria.EnumerateArray().Select(value => JsonSerializer.Serialize(value)).ToList()
                    : entry.Value.Criteria.EnumerateObject().Select(value => value.Name).ToList(),
            }).ToList(),
        };
    }

    public static SystemOneDecisionResponse ToResponse(SystemOneDecisionRequest request, LlmTckDecisionResult result, bool roundValues = false)
    {
        return new()
        {
            Model = result.ModelId,
            Answers = result.Answers.ToDictionary(entry => entry.Key, entry => ToAnswer(request.Questions[entry.Key], RoundedAnswer(roundValues, entry.Value)), StringComparer.Ordinal),
            Usage = new() { InputTokens = result.Usage.InputTokens, OutputTokens = result.Usage.OutputTokens },
        };
    }

    private static LlmTckDecisionAnswer RoundedAnswer(bool roundValues, LlmTckDecisionAnswer answer)
    {
        if (!roundValues) { return answer; }
        return answer with
        {
            Probability = Round(answer.Probability),
            Score = Round(answer.Score),
            Confidence = Round(answer.Confidence),
            Probabilities = answer.Probabilities.ToDictionary(entry => entry.Key, entry => Math.Round(entry.Value, _nativeProbabilityPrecision))
        };
    }

    private static double? Round(double? value)
    {
        return value.HasValue ? Math.Round(value.Value, _nativeProbabilityPrecision) : null;
    }

    private static SystemOneDecisionAnswer ToAnswer(SystemOneDecisionQuestion question, LlmTckDecisionAnswer answer)
    {
        return new()
        {
            Type = question.Type,
            Probability = answer.Probability,
            Choice = answer.Choice,
            Score = answer.Score,
            Confidence = answer.Confidence,
            Probabilities = answer.Kind == LlmTckDecisionKind.Predicate ? null : new(answer.Probabilities),
            Legend = answer.Kind == LlmTckDecisionKind.Score ? question.Criteria.EnumerateArray()
            .Select((value, index) => (index, value)).ToDictionary(entry => entry.index.ToString(CultureInfo.InvariantCulture), entry => entry.value.Clone()) : null,
        };
    }
}

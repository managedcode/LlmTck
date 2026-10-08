using System.Text.Json;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.SystemOne;

namespace ManagedCode.LlmTck.Cloudflare;

public static class CloudflareDecisionValidation
{
    public const int MaxQuestions = 64;
    public const int MaxQuestionIdLength = 100;

    public static LlmTckRequestValidationResult Validate(JsonElement body)
    {
        var shape = SystemOneDecisionMapper.Validate(body, SystemOneDecisionMapper.MaxChoices, SystemOneDecisionMapper.JevMaxScoreLevels);
        if (!shape.IsValid) { return shape; }
        var questions = body.GetProperty("questions").EnumerateObject().ToArray();
        if (!questions.All(question => HasRequiredInstructions(question.Value) && HasEnoughCriteria(question.Value)))
        { return new("Clef questions require instructions; choice and score criteria require at least two entries."); }
        return questions.Length <= MaxQuestions && questions.All(question => question.Name.Length <= MaxQuestionIdLength
            && question.Name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-'))
            ? new() : new("Clef supports at most 64 questions with ASCII letters, digits, underscore, dot and hyphen IDs of at most 100 characters.");
    }
    private static bool HasRequiredInstructions(JsonElement question)
    {
        return question.TryGetProperty("instructions", out var instructions) && (instructions.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            || (instructions.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(instructions.GetString())));
    }

    private static bool HasEnoughCriteria(JsonElement question)
    {
        return question.GetProperty("type").GetString() switch
        {
            SystemOneDecisionTypes.Choice => question.GetProperty("criteria").EnumerateObject().Count() >= 2,
            SystemOneDecisionTypes.Score => question.GetProperty("criteria").GetArrayLength() >= 2,
            _ => true,
        };
    }
}

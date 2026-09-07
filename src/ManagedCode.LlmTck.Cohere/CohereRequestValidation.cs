using System.Text.Json;
using ManagedCode.LlmTck.Providers;
using static ManagedCode.LlmTck.ProviderValidation.LlmTckJsonValidation;

namespace ManagedCode.LlmTck.Cohere;

public static class CohereRequestValidation
{
    public static LlmTckRequestValidationResult Validate(JsonElement body)
    {
        return Check(body, ValidateBody);
    }

    private static void ValidateBody(JsonElement body)
    {
        if (body.TryGetProperty("functions", out _) || body.TryGetProperty("function_call", out _))
        {
            throw new JsonException("Legacy function fields are unsupported.");
        }
        foreach (var tool in OptionalArray(body, "tools"))
        {
            Choice(tool.GetProperty("type"), "function");
            var function = tool.GetProperty("function"); Name(function); OptionalSchema(function, "parameters");
        }
        if (body.TryGetProperty("tool_choice", out var choice)) { Choice(choice, "REQUIRED", "NONE"); }
        if (body.TryGetProperty("response_format", out var format) && format.ValueKind != JsonValueKind.Null)
        {
            Choice(format.GetProperty("type"), "text", "json_object"); OptionalSchema(format, "schema");
        }
        foreach (var message in OptionalArray(body, "messages"))
        {
            foreach (var call in OptionalArray(message, "tool_calls", allowNull: true))
            {
                NonEmpty(call.GetProperty("id")); Name(call.GetProperty("function")); String(call.GetProperty("function").GetProperty("arguments"));
            }
        }
    }
}

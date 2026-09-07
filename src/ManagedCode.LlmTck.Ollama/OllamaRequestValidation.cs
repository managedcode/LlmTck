using System.Text.Json;
using ManagedCode.LlmTck.Providers;
using static ManagedCode.LlmTck.ProviderValidation.LlmTckJsonValidation;

namespace ManagedCode.LlmTck.Ollama;

public static class OllamaRequestValidation
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
        if (body.TryGetProperty("format", out var format))
        {
            if (format.ValueKind == JsonValueKind.String) { Choice(format, "json", ""); }
            else { Schema(format); }
        }
        foreach (var message in OptionalArray(body, "messages"))
        {
            foreach (var call in OptionalArray(message, "tool_calls", allowNull: true))
            {
                Name(call.GetProperty("function")); Object(call.GetProperty("function").GetProperty("arguments"));
            }
        }
    }
}

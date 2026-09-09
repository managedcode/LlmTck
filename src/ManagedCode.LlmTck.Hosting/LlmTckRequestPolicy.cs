using System.Globalization;
using System.Text.Json;
using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using Microsoft.AspNetCore.Http;

namespace ManagedCode.LlmTck.Hosting;

internal static class LlmTckRequestPolicy
{
    public static IResult? ValidateApiVersion(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/azure-openai/openai/deployments"))
        {
            var versions = context.Request.Query["api-version"];
            return versions.Count == 1 && IsDatedApiVersion(versions[0])
                ? null
                : Results.Json(OpenAiWireMapper.ToError("invalid_api_version",
                    "This route requires one dated api-version supplied by the Azure SDK (yyyy-MM-dd or yyyy-MM-dd-preview)."), statusCode: 400);
        }

        var expected = path.StartsWithSegments("/azure-openai/openai/v1/video") ? "preview"
            : path == "/microsoft-foundry/chat/completions" || path == "/microsoft-foundry/embeddings"
                || path == "/microsoft-foundry/models/chat/completions" || path == "/microsoft-foundry/models/embeddings" ? "2024-05-01-preview" : null;
        return expected is null || context.Request.Query["api-version"] == expected
            ? null
            : Results.Json(OpenAiWireMapper.ToError("invalid_api_version", $"This route requires api-version={expected}."), statusCode: 400);
    }

    private static bool IsDatedApiVersion(string? version)
    {
        var date = version.AsSpan();
        if (date.EndsWith("-preview", StringComparison.Ordinal))
        {
            date = date[..^"-preview".Length];
        }

        return DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    public static bool IsChatRequest<T>()
    {
        return typeof(T) == typeof(OpenAiChatCompletionRequest)
        || typeof(T) == typeof(OpenAiResponseRequest) || typeof(T) == typeof(AnthropicMessagesRequest)
        || typeof(T) == typeof(GeminiGenerateContentRequest) || typeof(T) == typeof(OllamaChatRequest)
        || typeof(T) == typeof(CohereChatRequest) || typeof(T) == typeof(BedrockConverseRequest)
        || typeof(T) == typeof(JsonElement);
    }

    public static bool HasUnsupportedChatFeatures(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in body.EnumerateObject())
        {
            var value = property.Value;
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            switch (property.Name)
            {
                case "tools":
                case "functions":
                    if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 0)
                    {
                        return true;
                    }
                    break;
                case "tool_choice":
                case "function_call":
                    if (value.ValueKind != JsonValueKind.String || value.GetString() is not ("none" or "auto"))
                    {
                        return true;
                    }
                    break;
                case "toolConfig":
                case "tool_config":
                case "output_config":
                case "outputConfig":
                    return true;
                case "response_format":
                case "format":
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("type", out var type)
                        || type.ValueKind != JsonValueKind.String || type.GetString() != "text")
                    {
                        return true;
                    }
                    break;
                case "responseMimeType":
                    if (value.ValueKind != JsonValueKind.String || value.GetString() != "text/plain")
                    {
                        return true;
                    }
                    break;
                case "responseSchema":
                case "responseJsonSchema":
                    return true;
                case "text":
                case "generationConfig":
                    if (HasUnsupportedChatFeatures(value))
                    {
                        return true;
                    }
                    break;
            }
        }

        return false;
    }
}

using System.Text.Json;
using Json.Schema;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed partial class LlmTckRuntime
{
    private static string? ValidateFixture(LlmTckChatRequest request, LlmTckScenarioResponse response)
    {
        if (response.Error is not null)
        {
            return null;
        }

        try
        {
            if (request.Tools is null || request.Tools.Any(tool => tool is null || string.IsNullOrWhiteSpace(tool.Name)))
            {
                return "Tool declarations require names.";
            }

            if (request.Tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != request.Tools.Count)
            {
                return "Tool names must be unique.";
            }

            foreach (var tool in request.Tools)
            {
                _ = LlmTckJsonSchema.Matches("{}", tool.ParametersJson);
            }

            if (request.ResponseSchemaJson is { } declaredSchema)
            {
                _ = LlmTckJsonSchema.Matches("{}", declaredSchema);
            }

            if (!request.AllowParallelToolCalls && response.ToolCalls.Count > 1)
            {
                return "The selected fixture contains parallel tool calls, which the request disabled.";
            }

            if (request.ToolChoice == LlmTckToolChoice.Required && response.ToolCalls.Count == 0)
            {
                return "The selected fixture must contain a tool call.";
            }

            if (request.ToolChoice == LlmTckToolChoice.None && response.ToolCalls.Count > 0)
            {
                return "The selected fixture contains a tool call but tool choice is none.";
            }

            foreach (var call in response.ToolCalls)
            {
                var definition = request.Tools.FirstOrDefault(tool => tool.Name == call.Name);
                if (definition is null || request.RequiredToolName is { } required && required != call.Name)
                {
                    return "The fixture calls a tool that was not selected or declared in the request.";
                }

                if (!LlmTckJsonSchema.Matches(call.ArgumentsJson, definition.ParametersJson))
                {
                    return "Fixture tool arguments do not match the declared parameter schema.";
                }
            }
            if (response.ToolCalls.Select(call => call.Id).Distinct(StringComparer.Ordinal).Count() != response.ToolCalls.Count)
            {
                return "Fixture tool call IDs must be unique.";
            }

            if (response.ToolCalls.Count == 0 && (request.RequireJson || request.ResponseSchemaJson is not null))
            {
                LlmTckJsonSchema.ValidateJson(response.Content);
                if (request.ResponseSchemaJson is { } schema && !LlmTckJsonSchema.Matches(response.Content, schema))
                {
                    return "The selected fixture does not match the requested JSON schema.";
                }

                if (response.StreamChunks.Count > 0 && string.Concat(response.StreamChunks) != response.Content)
                {
                    return "Structured-output stream chunks must concatenate to the validated JSON fixture.";
                }
            }
            return null;
        }
        catch (Exception exception) when (exception is JsonException or JsonSchemaException or ArgumentException)
        {
            return $"Invalid JSON or schema in request/fixture: {exception.Message}";
        }
    }
}

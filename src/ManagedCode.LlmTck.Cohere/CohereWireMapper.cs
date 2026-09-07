using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Cohere;

public static class CohereWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(CohereChatRequest request)
    {
        return new()
        {
            Tools = request.Tools.Select(tool => new LlmTckToolDefinition { Name = tool.Function.Name, Description = tool.Function.Description, ParametersJson = tool.Function.Parameters.GetRawText() }).ToList(),
            ToolChoice = request.ToolChoice switch { "REQUIRED" => LlmTckToolChoice.Required, "NONE" => LlmTckToolChoice.None, _ => LlmTckToolChoice.Auto },
            RequireJson = request.ResponseFormat?.Type == "json_object",
            ResponseSchemaJson = request.ResponseFormat?.Schema?.GetRawText(),
            ModelId = request.Model,
            Stream = request.Stream,
            Messages = request
                .Messages
                .Select(message => new LlmTckMessage
                {
                    Role = message.Role,
                    Content = message.TextContent,
                    ToolCallId = message.ToolCallId,
                    ToolCalls = message.ToolCalls?.Select(call => new LlmTckToolCall { Id = call.Id, Name = call.Function.Name, ArgumentsJson = call.Function.Arguments }).ToList() ?? [],
                })
                .ToList(),
        };
    }

    public static CohereChatResponse ToChatResponse(LlmTckChatResult result)
    {
        return new()
        {
            Id = CreateResponseId(),
            FinishReason = result.ToolCalls.Count > 0 ? "TOOL_CALL" : "COMPLETE",
            Message = new CohereAssistantMessage
            {
                Content = [new CohereContentBlock { Text = result.Content }],
                ToolCalls = result.ToolCalls.Count == 0 ? null : result.ToolCalls.Select(ToToolCall).ToList(),
            },
            Usage = CreateUsage(result.Usage),
        };
    }

    public static object ToMessageStartEvent()
    {
        return new
        {
            type = "message-start",
            id = CreateResponseId(),
            delta = new
            {
                message = new { role = "assistant" },
            },
        };
    }

    public static object ToContentStartEvent()
    {
        return new
        {
            type = "content-start",
            index = 0,
            delta = new
            {
                message = new
                {
                    content = new CohereContentBlock(),
                },
            },
        };
    }

    public static object ToContentDeltaEvent(string text)
    {
        return new
        {
            type = "content-delta",
            index = 0,
            delta = new
            {
                message = new
                {
                    content = new { text },
                },
            },
        };
    }

    public static object ToContentEndEvent()
    {
        return new
        {
            type = "content-end",
            index = 0,
        };
    }

    public static object ToMessageEndEvent(LlmTckChatResult result)
    {
        return new
        {
            type = "message-end",
            delta = new
            {
                finish_reason = result.ToolCalls.Count > 0 ? "TOOL_CALL" : "COMPLETE",
                usage = CreateUsage(result.Usage),
            },
        };
    }

    private static CohereToolCall ToToolCall(LlmTckToolCall call)
    {
        return new() { Id = call.Id, Function = new() { Name = call.Name, Arguments = call.ArgumentsJson } };
    }

    public static IEnumerable<object> ToToolStreamEvents(LlmTckChatResult result)
    {
        for (var index = 0; index < result.ToolCalls.Count; index++)
        {
            var call = result.ToolCalls[index];
            yield return new { type = "tool-call-start", index, delta = new { message = new { tool_calls = ToToolCall(call) with { Function = new() { Name = call.Name, Arguments = "" } } } } };
            yield return new { type = "tool-call-delta", index, delta = new { message = new { tool_calls = new { function = new { arguments = call.ArgumentsJson } } } } };
            yield return new { type = "tool-call-end", index };
        }
    }

    public static CohereEmbedResponse ToEmbedResponse(IReadOnlyList<IReadOnlyList<float>> vectors)
    {
        return new()
        {
            Id = CreateResponseId(),
            Embeddings = new CohereEmbeddings { FloatValues = vectors },
        };
    }

    public static CohereErrorResponse ToError(string message)
    {
        return new() { Message = message };
    }

    public static List<string> ReadEmbeddingInputs(CohereEmbedRequest request)
    {
        if (request.Texts is { Count: > 0 })
        {
            return [.. request.Texts];
        }

        if (request.Inputs.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return [];
        }

        return request
            .Inputs
            .EnumerateArray()
            .Select(CohereContentReader.ReadTextContent)
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .ToList();
    }

    private static CohereUsage CreateUsage(LlmTckTokenUsage usage)
    {
        var tokens = new CohereTokenUsage
        {
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
        };
        return new() { BilledUnits = tokens, Tokens = tokens };
    }

    private static string CreateResponseId()
    {
        return Guid.NewGuid().ToString();
    }

}

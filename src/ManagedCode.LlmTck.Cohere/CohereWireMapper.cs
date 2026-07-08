using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Cohere;

public static class CohereWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(CohereChatRequest request)
    {
        return new()
        {
            ModelId = request.Model,
            Stream = request.Stream,
            Messages = request
                .Messages
                .Select(message => new LlmTckMessage
                {
                    Role = message.Role,
                    Content = message.TextContent,
                })
                .ToList(),
        };
    }

    public static CohereChatResponse ToChatResponse(LlmTckChatResult result)
    {
        var outputTokens = LlmTckTokenCounter.CountTextTokens(result.Content);
        return new()
        {
            Id = CreateResponseId(),
            Message = new CohereAssistantMessage
            {
                Content = [new CohereContentBlock { Text = result.Content }],
            },
            Usage = CreateUsage(outputTokens),
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
                finish_reason = "COMPLETE",
                usage = CreateUsage(LlmTckTokenCounter.CountTextTokens(result.Content)),
            },
        };
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

    private static CohereUsage CreateUsage(int outputTokens)
    {
        var tokens = new CohereTokenUsage { OutputTokens = outputTokens };
        return new() { BilledUnits = tokens, Tokens = tokens };
    }

    private static string CreateResponseId()
    {
        return Guid.NewGuid().ToString();
    }

}

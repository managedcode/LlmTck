using System.Text;
using System.Text.Json;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Bedrock;

public static class BedrockWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(
        string modelId,
        BedrockConverseRequest request,
        bool stream
    )
    {
        var messages = request
            .Messages
            .Select(message => new LlmTckMessage
            {
                Role = message.Role,
                Content = ReadText(message.Content),
            })
            .ToList();

        var system = ReadText(request.System);
        if (!string.IsNullOrWhiteSpace(system))
        {
            messages.Insert(0, new LlmTckMessage { Role = "system", Content = system });
        }

        return new()
        {
            ModelId = modelId,
            Stream = stream,
            Messages = messages,
        };
    }

    public static LlmTckChatRequest ToRuntimeRequest(
        string modelId,
        JsonElement request,
        bool stream = false
    )
    {
        return new()
        {
            ModelId = modelId,
            Stream = stream,
            Messages =
            [
                new LlmTckMessage
                {
                    Role = "user",
                    Content = ReadInvokeInput(request),
                },
            ],
        };
    }

    public static BedrockConverseResponse ToConverseResponse(LlmTckChatResult result)
    {
        var outputTokens = CountTokens(result.Content);
        return new()
        {
            Output = new BedrockConverseOutput
            {
                Message = new BedrockMessage
                {
                    Role = "assistant",
                    Content = [new BedrockContentBlock { Text = result.Content }],
                },
            },
            Usage = new BedrockUsage
            {
                OutputTokens = outputTokens,
                TotalTokens = outputTokens,
            },
        };
    }

    public static BedrockTitanTextResponse ToTitanTextResponse(
        LlmTckChatResult result,
        string inputText
    )
    {
        return new()
        {
            InputTextTokenCount = CountTokens(inputText),
            Results =
            [
                new BedrockTitanTextResult
                {
                    TokenCount = CountTokens(result.Content),
                    OutputText = result.Content,
                },
            ],
        };
    }

    public static BedrockTitanEmbeddingResponse ToTitanEmbeddingResponse(
        IReadOnlyList<float> vector,
        string inputText
    )
    {
        return new()
        {
            Embedding = vector,
            InputTextTokenCount = CountTokens(inputText),
            EmbeddingsByType = new Dictionary<string, IReadOnlyList<float>>(StringComparer.Ordinal)
            {
                ["float"] = vector,
            },
        };
    }

    public static BedrockImageResponse ToImageResponse(string dataUri)
    {
        return new()
        {
            Images = [StripDataUriPrefix(dataUri)],
            FinishReasons = [null],
        };
    }

    public static object ToConverseMessageStartEvent()
    {
        return new { messageStart = new { role = "assistant" } };
    }

    public static object ToConverseContentBlockStartEvent()
    {
        return new
        {
            contentBlockStart = new { contentBlockIndex = 0, start = new { } },
        };
    }

    public static object ToConverseContentBlockDeltaEvent(string text)
    {
        return new
        {
            contentBlockDelta = new
            {
                contentBlockIndex = 0,
                delta = new { text },
            },
        };
    }

    public static object ToConverseContentBlockStopEvent()
    {
        return new { contentBlockStop = new { contentBlockIndex = 0 } };
    }

    public static object ToConverseMessageStopEvent()
    {
        return new { messageStop = new { stopReason = "end_turn" } };
    }

    public static object ToConverseMetadataEvent(LlmTckChatResult result)
    {
        var outputTokens = CountTokens(result.Content);
        return new
        {
            metadata = new
            {
                usage = new
                {
                    outputTokens,
                    totalTokens = outputTokens,
                },
                metrics = new { latencyMs = 0 },
            },
        };
    }

    public static object ToInvokeStreamChunk(string outputText)
    {
        var payload = JsonSerializer.Serialize(new { outputText });
        return new
        {
            chunk = new
            {
                bytes = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            },
        };
    }

    public static BedrockErrorResponse ToError(string message)
    {
        return new() { Message = message };
    }

    public static string ReadInvokeInput(JsonElement request)
    {
        if (request.ValueKind == JsonValueKind.String)
        {
            return request.GetString() ?? string.Empty;
        }

        if (request.ValueKind != JsonValueKind.Object)
        {
            return request.ToString();
        }

        foreach (var propertyName in new[] { "inputText", "prompt", "text" })
        {
            if (request.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        if (request.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(messages.EnumerateArray().Select(ReadMessageText));
        }

        return request.ToString();
    }

    public static string ReadText(IReadOnlyList<BedrockContentBlock> content)
    {
        return string.Concat(content.Select(block => block.Text).Where(text => text is not null));
    }

    private static string ReadMessageText(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            return message.ToString();
        }

        if (message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(
                content
                    .EnumerateArray()
                    .Select(block =>
                        block.ValueKind == JsonValueKind.Object
                        && block.TryGetProperty("text", out var text)
                            ? text.GetString()
                            : null
                    )
                    .Where(text => text is not null)
            );
        }

        return message.ToString();
    }

    private static string StripDataUriPrefix(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',', StringComparison.Ordinal);
        return commaIndex < 0 ? dataUri : dataUri[(commaIndex + 1)..];
    }

    private static int CountTokens(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Anthropic;

public static class AnthropicWireMapper
{
    public const string VersionHeaderName = "anthropic-version";
    public const string SupportedVersion = "2023-06-01";

    public static LlmTckChatRequest ToRuntimeRequest(AnthropicMessagesRequest request)
    {
        var messages = new List<LlmTckMessage>();
        var system = AnthropicContentReader.ReadTextContent(request.System);
        if (!string.IsNullOrWhiteSpace(system))
        {
            messages.Add(new LlmTckMessage { Role = "system", Content = system });
        }

        messages.AddRange(request.Messages.Select(message => new LlmTckMessage
        {
            Role = message.Role,
            Content = message.TextContent,
        }));

        return new()
        {
            ModelId = request.Model,
            Stream = request.Stream,
            Messages = messages,
        };
    }

    public static AnthropicMessageResponse ToMessageResponse(LlmTckChatResult result)
    {
        return new()
        {
            Id = CreateMessageId(),
            Model = result.ModelId,
            Content = [new AnthropicContentBlock { Text = result.Content }],
            Usage = new AnthropicUsage
            {
                InputTokens = 0,
                OutputTokens = LlmTckTokenCounter.CountTextTokens(result.Content),
            },
        };
    }

    public static AnthropicErrorResponse ToError(string type, string message)
    {
        return new()
        {
            Error = new AnthropicError
            {
                Type = type,
                Message = message,
            },
            RequestId = CreateRequestId(),
        };
    }

    public static object ToMessageStartEvent(LlmTckChatResult result)
    {
        return new
        {
            type = "message_start",
            message = new AnthropicMessageResponse
            {
                Id = CreateMessageId(),
                Model = result.ModelId,
                StopReason = null,
                Content = [],
                Usage = new AnthropicUsage { OutputTokens = 1 },
            },
        };
    }

    public static object ToContentBlockStartEvent()
    {
        return new
        {
            type = "content_block_start",
            index = 0,
            content_block = new AnthropicContentBlock(),
        };
    }

    public static object ToContentBlockDeltaEvent(string text)
    {
        return new
        {
            type = "content_block_delta",
            index = 0,
            delta = new
            {
                type = "text_delta",
                text,
            },
        };
    }

    public static object ToContentBlockStopEvent()
    {
        return new
        {
            type = "content_block_stop",
            index = 0,
        };
    }

    public static object ToMessageDeltaEvent(LlmTckChatResult result)
    {
        return new
        {
            type = "message_delta",
            delta = new
            {
                stop_reason = "end_turn",
                stop_sequence = (string?)null,
            },
            usage = new AnthropicUsage
            {
                OutputTokens = LlmTckTokenCounter.CountTextTokens(result.Content),
            },
        };
    }

    public static object ToMessageStopEvent()
    {
        return new
        {
            type = "message_stop",
        };
    }

    private static string CreateMessageId()
    {
        return $"msg_llmtck_{Guid.NewGuid():N}";
    }

    private static string CreateRequestId()
    {
        return $"req_llmtck_{Guid.NewGuid():N}";
    }

}

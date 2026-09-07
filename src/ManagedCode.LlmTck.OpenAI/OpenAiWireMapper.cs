using System.Text.Json;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.OpenAI;

public enum OpenAiCacheUsageShape
{
    None,
    PromptTokensDetails,
    PromptTokensDetailsWithCacheWriteTokens,
    DeepSeekPromptCache,
}

public static class OpenAiWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(
        OpenAiChatCompletionRequest request,
        LlmTckPromptCachePolicy promptCachePolicy = LlmTckPromptCachePolicy.None
    )
    {
        return OpenAiToolMapper.Apply(new()
        {
            ModelId = request.Model,
            AllowParallelToolCalls = request.ParallelToolCalls,
            Stream = request.Stream,
            PromptCachePolicy = promptCachePolicy,
            PromptCacheKey = ReadPromptCacheKey(
                request.PromptCacheKey,
                request.SessionId,
                promptCachePolicy
            ),
            Messages = request
                .Messages
                .Select(message => new LlmTckMessage
                {
                    Role = message.Role,
                    Content = message.TextContent,
                    ToolCallId = message.ToolCallId,
                    ToolCalls = message.ToolCalls?.Select(OpenAiToolMapper.ToRuntime).ToList() ?? [],
                })
                .ToList(),
        }, request.Tools, request.ToolChoice, request.ResponseFormat);
    }

    public static LlmTckChatRequest ToRuntimeRequest(
        OpenAiResponseRequest request,
        LlmTckPromptCachePolicy promptCachePolicy = LlmTckPromptCachePolicy.None
    )
    {
        var messages = ReadResponseInputMessages(request.Input);
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            messages.Insert(
                0,
                new LlmTckMessage
                {
                    Role = "system",
                    Content = request.Instructions,
                }
            );
        }

        return OpenAiToolMapper.Apply(new()
        {
            ModelId = request.Model,
            AllowParallelToolCalls = request.ParallelToolCalls,
            Stream = request.Stream,
            PromptCachePolicy = promptCachePolicy,
            PromptCacheKey = ReadPromptCacheKey(
                request.PromptCacheKey,
                request.SessionId,
                promptCachePolicy
            ),
            Messages = messages,
        }, request.Tools.Select(tool => new OpenAiTool { Type = tool.Type, Function = new() { Name = tool.Name, Description = tool.Description, Parameters = tool.Parameters } }).ToList(),
            request.ToolChoice.ValueKind == JsonValueKind.Object ? JsonSerializer.SerializeToElement(new { function = request.ToolChoice }) : request.ToolChoice,
            request.Text?.Format is { } format ? new() { Type = format.Type, JsonSchema = format.Type == "json_schema" ? new() { Schema = format.Schema } : null } : null);
    }

    public static OpenAiChatCompletionResponse ToChatResponse(
        LlmTckChatResult result,
        OpenAiCacheUsageShape cacheUsageShape = OpenAiCacheUsageShape.None
    )
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new OpenAiChatCompletionResponse
        {
            Id = CreateResponseId(),
            Created = now,
            Model = result.ModelId,
            Choices =
            [
                new OpenAiChatChoice
                {
                    Index = 0,
                    Message = OpenAiChatMessage.FromText("assistant", result.Content) with { ToolCalls = result.ToolCalls.Count > 0 ? result.ToolCalls.Select(call => OpenAiToolMapper.ToWire(call)).ToList() : null },
                    FinishReason = result.ToolCalls.Count > 0 ? "tool_calls" : "stop",
                },
            ],
            Usage = CreateUsage(result.Usage, cacheUsageShape),
        };
    }

    public static OpenAiChatCompletionChunk ToChatChunk(
        LlmTckChatResult result,
        string content,
        string responseId,
        long created,
        string? finishReason = null
    )
    {
        return new()
        {
            Id = responseId,
            Created = created,
            Model = result.ModelId,
            Choices =
            [
                new OpenAiChatChunkChoice
                {
                    Index = 0,
                    Delta = OpenAiChatMessage.FromText("assistant", content) with
                    {
                        ToolCalls = finishReason is null && result.ToolCalls.Count > 0
                            ? result.ToolCalls.Select((call, index) => OpenAiToolMapper.ToWire(call, index)).ToList() : null,
                    },
                    FinishReason = finishReason,
                },
            ],
        };
    }

    public static OpenAiResponse ToResponse(
        LlmTckChatResult result,
        string responseId,
        long created,
        OpenAiCacheUsageShape cacheUsageShape = OpenAiCacheUsageShape.None
    )
    {
        return new()
        {
            Id = responseId,
            CreatedAt = created,
            Model = result.ModelId,
            Output = CreateResponseOutput(result),
            Usage = new OpenAiResponseUsage
            {
                InputTokens = result.Usage.InputTokens,
                OutputTokens = result.Usage.OutputTokens,
                TotalTokens = result.Usage.TotalTokens,
                InputTokensDetails = CreateInputTokensDetails(
                    result.Usage,
                    cacheUsageShape
                ),
                OutputTokensDetails = result.Usage.ReasoningTokens > 0
                    ? new OpenAiOutputTokensDetails
                    {
                        ReasoningTokens = result.Usage.ReasoningTokens,
                    }
                    : null,
            },
        };
    }

    private static List<OpenAiResponseOutputMessage> CreateResponseOutput(LlmTckChatResult result)
    {
        var output = new List<OpenAiResponseOutputMessage>();
        if (result.Content.Length > 0 || result.ToolCalls.Count == 0)
        {
            output.Add(CreateOutputMessage(result.Content, completed: true));
        }

        output.AddRange(result.ToolCalls.Select(call => new OpenAiResponseOutputMessage
        { Type = "function_call", Id = "fc_" + call.Id, CallId = call.Id, Name = call.Name, Arguments = call.ArgumentsJson }));
        return output;
    }

    public static IEnumerable<object> ToResponseStreamEvents(
        LlmTckChatResult result, string responseId, long created,
        OpenAiCacheUsageShape cacheUsageShape = OpenAiCacheUsageShape.None)
    {
        var response = ToResponse(result, responseId, created, cacheUsageShape);
        var sequence = 0;
        yield return new { type = "response.created", sequence_number = sequence++, response = response with { Status = "in_progress", Output = [] } };
        yield return new { type = "response.in_progress", sequence_number = sequence++, response = response with { Status = "in_progress", Output = [] } };
        for (var index = 0; index < response.Output.Count; index++)
        {
            var item = response.Output[index];
            yield return new { type = "response.output_item.added", sequence_number = sequence++, output_index = index, item = item with { Status = "in_progress", Content = [], Arguments = item.Arguments is null ? null : "" } };
            if (item.Type == "function_call")
            {
                yield return new { type = "response.function_call_arguments.delta", sequence_number = sequence++, item_id = item.Id, output_index = index, delta = item.Arguments };
                yield return new { type = "response.function_call_arguments.done", sequence_number = sequence++, item_id = item.Id, output_index = index, arguments = item.Arguments };
            }
            else
            {
                yield return new { type = "response.content_part.added", sequence_number = sequence++, item_id = item.Id, output_index = index, content_index = 0, part = new OpenAiResponseOutputText() };
                IReadOnlyList<string> chunks = result.StreamChunks.Count > 0 ? result.StreamChunks : [result.Content];
                foreach (var chunk in chunks)
                {
                    yield return new { type = "response.output_text.delta", sequence_number = sequence++, item_id = item.Id, output_index = index, content_index = 0, delta = chunk, logprobs = Array.Empty<object>() };
                }

                yield return new { type = "response.output_text.done", sequence_number = sequence++, item_id = item.Id, output_index = index, content_index = 0, text = result.Content, logprobs = Array.Empty<object>() };
                yield return new { type = "response.content_part.done", sequence_number = sequence++, item_id = item.Id, output_index = index, content_index = 0, part = item.Content[0] };
            }
            yield return new { type = "response.output_item.done", sequence_number = sequence++, output_index = index, item };
        }
        yield return new { type = "response.completed", sequence_number = sequence, response };
    }

    public static object ToResponseCreatedEvent(
        LlmTckChatResult result,
        string responseId,
        long created
    )
    {
        return new
        {
            type = "response.created",
            response = new
            {
                id = responseId,
                @object = "response",
                created_at = created,
                model = result.ModelId,
                status = "in_progress",
            },
        };
    }

    public static object ToResponseOutputItemAddedEvent(string responseId)
    {
        return new
        {
            type = "response.output_item.added",
            response_id = responseId,
            output_index = 0,
            item = CreateOutputMessage(string.Empty, completed: false),
        };
    }

    public static object ToResponseContentPartAddedEvent(string responseId)
    {
        return new
        {
            type = "response.content_part.added",
            response_id = responseId,
            output_index = 0,
            content_index = 0,
            part = new OpenAiResponseOutputText(),
        };
    }

    public static object ToResponseContentPartDeltaEvent(string responseId, string delta)
    {
        return new
        {
            type = "response.output_text.delta",
            response_id = responseId,
            output_index = 0,
            content_index = 0,
            delta,
        };
    }

    public static object ToResponseOutputItemDoneEvent(string responseId, LlmTckChatResult result)
    {
        return new
        {
            type = "response.output_item.done",
            response_id = responseId,
            output_index = 0,
            item = CreateOutputMessage(result.Content, completed: true),
        };
    }

    public static object ToResponseDoneEvent(
        LlmTckChatResult result,
        string responseId,
        long created,
        OpenAiCacheUsageShape cacheUsageShape = OpenAiCacheUsageShape.None
    )
    {
        return new
        {
            type = "response.completed",
            response = ToResponse(result, responseId, created, cacheUsageShape),
        };
    }

    public static object ToModelsResponse(IEnumerable<LlmTckModel> models)
    {
        return new
        {
            @object = "list",
            data = models.Select(model => new
            {
                id = model.Id,
                @object = "model",
                owned_by = model.OwnedBy,
                metadata = new { kind = model.Kind.ToString().ToLowerInvariant() },
            }),
        };
    }

    public static OpenAiEmbeddingResponse ToEmbeddingResponse(
        string model,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        bool encodeAsBase64 = false
    )
    {
        return new()
        {
            Id = CreateResponseId(),
            Model = model,
            Data = vectors
                .Select((vector, index) => OpenAiEmbeddingData.FromVector(index, vector, encodeAsBase64))
                .ToList(),
            Usage = new OpenAiUsage
            {
                PromptTokens = vectors.Count,
                TotalTokens = vectors.Count,
            },
        };
    }

    public static OpenAiImageGenerationResponse ToImageResponse(
        string dataUri,
        string? background = null,
        string? outputFormat = null,
        string? quality = null,
        string? size = null
    )
    {
        return new OpenAiImageGenerationResponse
        {
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Background = background,
            OutputFormat = outputFormat,
            Quality = quality,
            Size = size,
            Data = [new OpenAiImageData { Base64Json = ReadBase64Payload(dataUri) }],
        };
    }

    public static object ToImageStreamingEvent(
        string eventType,
        string dataUri,
        bool completed,
        string? background,
        string? outputFormat,
        string? quality,
        string? size
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = eventType,
            ["b64_json"] = ReadBase64Payload(dataUri),
            ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["size"] = size ?? "1024x1024",
            ["quality"] = quality ?? "auto",
            ["background"] = background ?? "auto",
            ["output_format"] = outputFormat ?? "png",
        };

        if (completed)
        {
            payload["usage"] = new
            {
                total_tokens = 1,
                input_tokens = 1,
                output_tokens = 0,
                input_tokens_details = new
                {
                    text_tokens = 1,
                    image_tokens = 0,
                },
            };
        }
        else
        {
            payload["partial_image_index"] = 0;
        }

        return payload;
    }

    public static OpenAiVideoResponse ToVideoResponse(LlmTckVideoResult result)
    {
        return new()
        {
            Id = result.VideoId,
            Status = "completed",
            Progress = 100,
            RemixedFromVideoId = result.RemixedFromVideoId,
            Model = result.ModelId,
            Prompt = result.Prompt,
            CreatedAt = result.CreatedAt,
            CompletedAt = result.CreatedAt,
            ExpiresAt = result.CreatedAt + 172800,
            Seconds = result.Seconds,
            Size = result.Size,
        };
    }

    public static OpenAiVideoListResponse ToVideoListResponse(LlmTckVideoResult result)
    {
        var video = ToVideoResponse(result);
        return new()
        {
            Data = [video],
            FirstId = video.Id,
            LastId = video.Id,
        };
    }

    public static OpenAiVideoDeleteResponse ToVideoDeleteResponse(string videoId)
    {
        return new() { Id = videoId };
    }

    public static OpenAiVideoCharacterResponse ToVideoCharacterResponse(
        string characterId,
        string name,
        long createdAt
    )
    {
        return new()
        {
            Id = characterId,
            CreatedAt = createdAt,
            Name = name,
        };
    }

    public static AzureVideoGenerationJobResponse ToAzureVideoGenerationJobResponse(
        LlmTckVideoResult result
    )
    {
        var (width, height) = ReadVideoSize(result.Size);
        var generation = ToAzureVideoGenerationResponse(result);
        return new()
        {
            Id = result.VideoId,
            CreatedAt = result.CreatedAt,
            FinishedAt = result.CreatedAt,
            ExpiresAt = result.CreatedAt + 172800,
            Generations = [generation],
            Prompt = result.Prompt,
            Model = result.ModelId,
            NSeconds = int.TryParse(result.Seconds, out var seconds) ? seconds : 4,
            Height = height,
            Width = width,
        };
    }

    public static AzureVideoGenerationResponse ToAzureVideoGenerationResponse(
        LlmTckVideoResult result
    )
    {
        var (width, height) = ReadVideoSize(result.Size);
        return new()
        {
            Id = result.GenerationId,
            JobId = result.VideoId,
            CreatedAt = result.CreatedAt,
            Width = width,
            Height = height,
            NSeconds = int.TryParse(result.Seconds, out var seconds) ? seconds : 4,
            Prompt = result.Prompt,
        };
    }

    public static AzureVideoGenerationJobListResponse ToAzureVideoGenerationJobListResponse(
        LlmTckVideoResult result
    )
    {
        var job = ToAzureVideoGenerationJobResponse(result);
        return new()
        {
            Data = [job],
            FirstId = job.Id,
            LastId = job.Id,
        };
    }

    public static OpenAiAudioTranscriptionResponse ToAudioTranscriptionResponse(
        LlmTckTranscriptionResult result
    )
    {
        return new() { Text = result.Text };
    }

    public static GroqAudioTranscriptionResponse ToGroqAudioTranscriptionResponse(
        LlmTckTranscriptionResult result
    )
    {
        return new()
        {
            Text = result.Text,
            XGroq = new GroqRequestMetadata { Id = $"req_{Guid.NewGuid():N}" },
        };
    }

    public static OpenAiVerboseAudioTranscriptionResponse ToVerboseAudioTranscriptionResponse(
        LlmTckTranscriptionResult result
    )
    {
        return new() { Text = result.Text };
    }

    public static object ToTranscriptTextDeltaEvent(string delta)
    {
        return new
        {
            type = "transcript.text.delta",
            delta,
        };
    }

    public static object ToTranscriptTextDoneEvent(LlmTckTranscriptionResult result)
    {
        return new
        {
            type = "transcript.text.done",
            text = result.Text,
        };
    }

    public static OpenAiErrorResponse ToError(string code, string message)
    {
        return new()
        {
            Error = new OpenAiError
            {
                Code = code,
                Message = message,
            },
        };
    }

    public static string CreateResponseId()
    {
        return $"llmtck-{Guid.NewGuid():N}";
    }

    private static OpenAiUsage CreateUsage(
        LlmTckTokenUsage usage,
        OpenAiCacheUsageShape cacheUsageShape = OpenAiCacheUsageShape.None
    )
    {
        return new()
        {
            PromptTokens = usage.InputTokens,
            CompletionTokens = usage.OutputTokens,
            TotalTokens = usage.TotalTokens,
            PromptTokensDetails = CreatePromptTokensDetails(usage, cacheUsageShape),
            CompletionTokensDetails = usage.ReasoningTokens > 0
                ? new OpenAiCompletionTokensDetails
                {
                    ReasoningTokens = usage.ReasoningTokens,
                }
                : null,
            PromptCacheHitTokens = cacheUsageShape == OpenAiCacheUsageShape.DeepSeekPromptCache
                ? usage.CachedInputTokens
                : null,
            PromptCacheMissTokens = cacheUsageShape == OpenAiCacheUsageShape.DeepSeekPromptCache
                ? Math.Max(0, usage.InputTokens - usage.CachedInputTokens)
                : null,
        };
    }

    private static OpenAiPromptTokensDetails? CreatePromptTokensDetails(
        LlmTckTokenUsage usage,
        OpenAiCacheUsageShape cacheUsageShape
    )
    {
        return cacheUsageShape switch
        {
            OpenAiCacheUsageShape.PromptTokensDetails => new OpenAiPromptTokensDetails
            {
                CachedTokens = usage.CachedInputTokens,
            },
            OpenAiCacheUsageShape.PromptTokensDetailsWithCacheWriteTokens => new OpenAiPromptTokensDetails
            {
                CachedTokens = usage.CachedInputTokens,
                CacheWriteTokens = usage.CacheCreationInputTokens,
            },
            _ => null,
        };
    }

    private static OpenAiInputTokensDetails? CreateInputTokensDetails(
        LlmTckTokenUsage usage,
        OpenAiCacheUsageShape cacheUsageShape
    )
    {
        return cacheUsageShape switch
        {
            OpenAiCacheUsageShape.PromptTokensDetails => new OpenAiInputTokensDetails
            {
                CachedTokens = usage.CachedInputTokens,
            },
            OpenAiCacheUsageShape.PromptTokensDetailsWithCacheWriteTokens => new OpenAiInputTokensDetails
            {
                CachedTokens = usage.CachedInputTokens,
                CacheWriteTokens = usage.CacheCreationInputTokens,
            },
            _ => null,
        };
    }

    private static string? ReadPromptCacheKey(
        string? promptCacheKey,
        string? sessionId,
        LlmTckPromptCachePolicy promptCachePolicy
    )
    {
        if (promptCachePolicy == LlmTckPromptCachePolicy.OpenRouter
            && !string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return string.IsNullOrWhiteSpace(promptCacheKey) ? null : promptCacheKey;
    }

    private static (int Width, int Height) ReadVideoSize(string size)
    {
        var parts = size.Split('x', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], out var width)
            && int.TryParse(parts[1], out var height)
            ? (width, height)
            : (1280, 720);
    }

    private static string ReadBase64Payload(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',', StringComparison.Ordinal);
        return commaIndex >= 0 ? dataUri[(commaIndex + 1)..] : dataUri;
    }

    private static OpenAiResponseOutputMessage CreateOutputMessage(
        string content,
        bool completed
    )
    {
        return new()
        {
            Id = $"msg_{Guid.NewGuid():N}",
            Status = completed ? "completed" : "in_progress",
            Content = [new OpenAiResponseOutputText { Text = content }],
        };
    }

    private static List<LlmTckMessage> ReadResponseInputMessages(JsonElement input)
    {
        return input.ValueKind switch
        {
            JsonValueKind.String =>
            [
                new LlmTckMessage
                {
                    Role = "user",
                    Content = input.GetString() ?? string.Empty,
                },
            ],
            JsonValueKind.Array => input
                .EnumerateArray()
                .Select(ReadResponseInputMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message.Content) || message.ToolCalls.Count > 0 || message.ToolCallId is not null)
                .ToList(),
            JsonValueKind.Object =>
            [
                ReadResponseInputMessage(input),
            ],
            _ => [],
        };
    }

    private static LlmTckMessage ReadResponseInputMessage(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return new LlmTckMessage
            {
                Role = "user",
                Content = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString(),
            };
        }

        if (item.TryGetProperty("type", out var itemType))
        {
            if (itemType.GetString() == "function_call")
            {
                return new() { Role = "assistant", ToolCalls = [new() { Id = item.GetProperty("call_id").GetString()!, Name = item.GetProperty("name").GetString()!, ArgumentsJson = item.GetProperty("arguments").GetString()! }] };
            }

            if (itemType.GetString() == "function_call_output")
            {
                return new() { Role = "tool", ToolCallId = item.GetProperty("call_id").GetString(), Content = ReadResponseInputContent(item.GetProperty("output")) };
            }
        }
        var role = item.TryGetProperty("role", out var roleElement)
            ? roleElement.GetString() ?? "user"
            : "user";

        if (item.TryGetProperty("content", out var content))
        {
            return new LlmTckMessage
            {
                Role = role,
                Content = ReadResponseInputContent(content),
            };
        }

        if (item.TryGetProperty("text", out var text))
        {
            return new LlmTckMessage
            {
                Role = role,
                Content = ReadResponseInputContent(text),
            };
        }

        return new LlmTckMessage
        {
            Role = role,
            Content = item.ToString(),
        };
    }

    private static string ReadResponseInputContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Concat(content.EnumerateArray().Select(ReadResponseInputContent)),
            JsonValueKind.Object when content.TryGetProperty("text", out var text) =>
                ReadResponseInputContent(text),
            JsonValueKind.Object when content.TryGetProperty("content", out var nested) =>
                ReadResponseInputContent(nested),
            JsonValueKind.Object when content.TryGetProperty("type", out var type)
                && type.GetString() is "input_text" or "output_text"
                && content.TryGetProperty("text", out var typedText) =>
                ReadResponseInputContent(typedText),
            JsonValueKind.Object when content.TryGetProperty("type", out _) => string.Empty,
            JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
            _ => content.ToString(),
        };
    }

}

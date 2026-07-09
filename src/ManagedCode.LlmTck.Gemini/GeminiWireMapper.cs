using System.Text.Json;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Gemini;

public static class GeminiWireMapper
{
    public static LlmTckChatRequest ToRuntimeRequest(
        string model,
        GeminiGenerateContentRequest request,
        bool stream
    )
    {
        var messages = request
            .Contents
            .Select(content => new LlmTckMessage
            {
                Role = NormalizeRole(content.Role),
                Content = ReadText(content),
            })
            .ToList();

        if (request.SystemInstruction is not null)
        {
            messages.Insert(
                0,
                new LlmTckMessage
                {
                    Role = "system",
                    Content = ReadText(request.SystemInstruction),
                }
            );
        }

        return new()
        {
            ModelId = model,
            Stream = stream,
            PromptCachePolicy = LlmTckPromptCachePolicy.Gemini,
            Messages = messages,
        };
    }

    public static GeminiGenerateContentResponse ToGenerateContentResponse(
        LlmTckChatResult result,
        string content,
        string? finishReason = "STOP"
    )
    {
        var outputTokens = string.IsNullOrEmpty(content) && finishReason is not null
            ? result.Usage.OutputTokens
            : LlmTckTokenCounter.CountTextTokens(content);
        return new()
        {
            Candidates =
            [
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Role = "model",
                        Parts = [new GeminiPart { Text = content }],
                    },
                    FinishReason = finishReason,
                    Index = 0,
                },
            ],
            UsageMetadata = new GeminiUsageMetadata
            {
                PromptTokenCount = result.Usage.InputTokens,
                CachedContentTokenCount = result.Usage.CachedInputTokens,
                CandidatesTokenCount = outputTokens,
                TotalTokenCount = result.Usage.InputTokens + outputTokens,
            },
            ModelVersion = result.ModelId,
            ResponseId = CreateResponseId(),
        };
    }

    public static GeminiEmbedContentResponse ToEmbedContentResponse(IReadOnlyList<float> vector)
    {
        return new()
        {
            Embedding = new GeminiContentEmbedding { Values = vector },
            UsageMetadata = new GeminiUsageMetadata
            {
                PromptTokenCount = 1,
                TotalTokenCount = 1,
            },
        };
    }

    public static GeminiOperationResponse ToVideoOperationResponse(
        string model,
        LlmTckVideoResult result,
        string videoUri,
        bool done
    )
    {
        return new()
        {
            Name = CreateVideoOperationName(model, result.GenerationId),
            Done = done,
            Response = done
                ? new GeminiVideoOperationResponse
                {
                    GenerateVideoResponse = new GeminiGenerateVideoResponse
                    {
                        UsageMetadata = ToUsageMetadata(result.Usage),
                        GeneratedSamples =
                        [
                            new GeminiGeneratedVideoSample
                            {
                                Video = new GeminiGeneratedVideo
                                {
                                    Uri = videoUri,
                                    MimeType = result.MediaType,
                                },
                            },
                        ],
                    },
                }
                : null,
        };
    }

    public static GeminiFileResponse ToFileResponse(
        string fileId,
        LlmTckVideoResult result,
        string fileUri,
        string downloadUri
    )
    {
        return new()
        {
            Name = CreateFileName(fileId),
            DisplayName = $"{fileId}.mp4",
            MimeType = result.MediaType,
            SizeBytes = result.Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Uri = fileUri,
            DownloadUri = downloadUri,
            VideoMetadata = new GeminiVideoFileMetadata
            {
                VideoDuration = $"{result.Seconds}s",
            },
        };
    }

    public static GeminiErrorResponse ToError(int statusCode, string message)
    {
        return new()
        {
            Error = new GeminiError
            {
                Code = statusCode,
                Message = message,
                Status = ToGoogleStatus(statusCode),
            },
        };
    }

    public static string ReadText(GeminiContent content)
    {
        return string.Concat(
            content
                .Parts
                .Select(part => part.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
        );
    }

    public static string ReadPredictPrompt(GeminiPredictLongRunningRequest request)
    {
        return request
            .Instances
            .Select(ReadPrompt)
            .FirstOrDefault(prompt => !string.IsNullOrWhiteSpace(prompt)) ?? string.Empty;
    }

    public static string CreateVideoOperationName(string model, string operationId)
    {
        return $"models/{model}/operations/{operationId}";
    }

    public static string CreateFileName(string fileId)
    {
        return $"files/{fileId}";
    }

    private static GeminiUsageMetadata ToUsageMetadata(LlmTckTokenUsage usage)
    {
        return new()
        {
            PromptTokenCount = usage.InputTokens,
            CachedContentTokenCount = usage.CachedInputTokens,
            CandidatesTokenCount = usage.OutputTokens,
            TotalTokenCount = usage.TotalTokens,
        };
    }

    private static string ReadPrompt(JsonElement instance)
    {
        return instance.ValueKind == JsonValueKind.Object
            && instance.TryGetProperty("prompt", out var prompt)
            && prompt.ValueKind == JsonValueKind.String
            ? prompt.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeRole(string role)
    {
        return string.Equals(role, "model", StringComparison.OrdinalIgnoreCase)
            ? "assistant"
            : role;
    }

    private static string ToGoogleStatus(int statusCode)
    {
        return statusCode switch
        {
            400 => "INVALID_ARGUMENT",
            401 => "UNAUTHENTICATED",
            403 => "PERMISSION_DENIED",
            404 => "NOT_FOUND",
            409 => "ABORTED",
            429 => "RESOURCE_EXHAUSTED",
            >= 500 => "INTERNAL",
            _ => "INVALID_ARGUMENT",
        };
    }

    private static string CreateResponseId()
    {
        return $"gemini-{Guid.NewGuid():N}";
    }

}

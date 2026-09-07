using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public sealed record LlmTckChatResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public string? ScenarioId { get; init; }

    public List<LlmTckToolCall> ToolCalls { get; init; } = [];

    public string Content { get; init; } = string.Empty;

    public List<string> StreamChunks { get; init; } = [];

    public LlmTckTokenUsage Usage { get; init; } = new();

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static LlmTckChatResult Success(
        string modelId,
        string scenarioId,
        string content,
        IEnumerable<string> streamChunks,
        LlmTckTokenUsage? usage = null
    )
    {
        return new()
        {
            IsSuccess = true,
            ModelId = modelId,
            ScenarioId = scenarioId,
            Content = content,
            StreamChunks = [.. streamChunks],
            Usage = usage ?? new LlmTckTokenUsage(),
        };
    }

    public static LlmTckChatResult Failure(
        string modelId,
        int statusCode,
        string errorCode,
        string errorMessage,
        string? scenarioId = null,
        LlmTckTokenUsage? usage = null
    )
    {
        return new()
        {
            IsSuccess = false,
            ModelId = modelId,
            ScenarioId = scenarioId,
            StatusCode = statusCode,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Usage = usage ?? new LlmTckTokenUsage(),
        };
    }
}

public sealed record LlmTckEmbeddingResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.TextEmbedding3Small;

    public List<List<float>> Vectors { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record LlmTckImageResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.GptImage1;

    public string DataUri { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record LlmTckAudioResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt4OMiniTts;

    public byte[] Bytes { get; init; } = [];

    public string MediaType { get; init; } = "audio/mpeg";

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record LlmTckTranscriptionResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt4OMiniTts;

    public string Text { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed record LlmTckVideoResult
{
    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; } = 200;

    public string ModelId { get; init; } = LlmTckKnownModelIds.Sora2;

    public string Prompt { get; init; } = string.Empty;

    public string VideoId { get; init; } = "video_llm_tck";

    public string GenerationId { get; init; } = "gen_llm_tck";

    public string? RemixedFromVideoId { get; init; }

    public byte[] Bytes { get; init; } = [];

    public string MediaType { get; init; } = "video/mp4";

    public long CreatedAt { get; init; }

    public string Size { get; init; } = "1280x720";

    public string Seconds { get; init; } = "4";

    public LlmTckTokenUsage Usage { get; init; } = new();

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

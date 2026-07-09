using System.Text.Json.Serialization;
using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiAudioSpeechRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = LlmTckKnownModelIds.Gpt4OMiniTts;

    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;

    [JsonPropertyName("voice")]
    public string Voice { get; init; } = string.Empty;

    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; init; }
}

public sealed record OpenAiAudioTranscriptionResponse
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record GroqAudioTranscriptionResponse
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("x_groq")]
    public GroqRequestMetadata XGroq { get; init; } = new();
}

public sealed record GroqRequestMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

public sealed record OpenAiVerboseAudioTranscriptionResponse
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("duration")]
    public double Duration { get; init; }

    [JsonPropertyName("segments")]
    public IReadOnlyList<object> Segments { get; init; } = [];
}

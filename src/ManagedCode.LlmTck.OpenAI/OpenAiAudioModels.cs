using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiAudioSpeechRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "llm-tck-audio";

    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;

    [JsonPropertyName("voice")]
    public string Voice { get; init; } = "alloy";
}

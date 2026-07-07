using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiImageGenerationRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "llm-tck-image";

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
}

public sealed record OpenAiImageGenerationResponse
{
    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("data")]
    public List<OpenAiImageData> Data { get; init; } = [];
}

public sealed record OpenAiImageData
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("b64_json")]
    public string? Base64Json { get; init; }
}

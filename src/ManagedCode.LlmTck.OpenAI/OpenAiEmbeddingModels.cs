using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.LlmTck.OpenAI;

public sealed record OpenAiEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "llm-tck-embedding";

    [JsonPropertyName("input")]
    public JsonElement Input { get; init; }
}

public sealed record OpenAiEmbeddingResponse
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "list";

    [JsonPropertyName("model")]
    public string Model { get; init; } = "llm-tck-embedding";

    [JsonPropertyName("data")]
    public List<OpenAiEmbeddingData> Data { get; init; } = [];
}

public sealed record OpenAiEmbeddingData
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "embedding";

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("embedding")]
    public List<float> Embedding { get; init; } = [];
}

using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Configuration;

public sealed record LlmTckConfiguration
{
    public List<LlmTckModel> Models { get; init; } = [];

    public List<LlmTckScenario> ChatScenarios { get; init; } = [];

    public string? RequiredBearerToken { get; init; }

    public List<float> DefaultEmbeddingVector { get; init; } = [0.125f, 0.25f, 0.5f];

    public string DefaultImageDataUri { get; init; } =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGOSHzRgAAAAABJRU5ErkJggg==";

    public byte[] DefaultAudioBytes { get; init; } =
        Convert.FromBase64String("UklGRiQAAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQAAAAA=");

    public string DefaultAudioMediaType { get; init; } = "audio/wav";

    public static LlmTckConfiguration CreateDefault()
    {
        return new()
        {
            Models =
            [
                new LlmTckModel { Id = "llm-tck-chat", Kind = LlmTckModelKind.Chat },
                new LlmTckModel { Id = "llm-tck-embedding", Kind = LlmTckModelKind.Embedding },
                new LlmTckModel { Id = "llm-tck-image", Kind = LlmTckModelKind.Image },
                new LlmTckModel { Id = "llm-tck-audio", Kind = LlmTckModelKind.Audio },
            ],
        };
    }
}

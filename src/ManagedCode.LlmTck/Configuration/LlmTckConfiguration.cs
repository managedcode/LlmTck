using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Configuration;

public sealed record LlmTckConfiguration
{
    /// <summary>Maximum retained prompt-cache prefixes. Zero disables prompt caching.</summary>
    public int MaxPromptCacheEntries { get; init; } = 4096;

    /// <summary>Maximum stored video jobs across providers. Zero rejects all new jobs.</summary>
    public int MaxVideoJobs { get; init; } = 256;

    /// <summary>Maximum total retained video payload bytes. Overflow is rejected without eviction.</summary>
    public long MaxVideoBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>Maximum stored chat responses. Zero disables response storage.</summary>
    public int MaxStoredChatResponses { get; init; } = 256;

    /// <summary>Maximum total retained chat-history bytes including bounded record overhead.</summary>
    public long MaxStoredChatResponseBytes { get; init; } = 16 * 1024 * 1024;

    public List<LlmTckModel> Models { get; init; } = [];

    public List<ManagedCode.LlmTck.Decisions.LlmTckDecisionScenario> DecisionScenarios { get; init; } = [];

    public List<LlmTckScenario> ChatScenarios { get; init; } = [];

    public List<LlmTckScenarioDataset> Datasets { get; init; } = [];

    public string? RequiredBearerToken { get; init; }

    public LlmTckFaultSimulation FaultSimulation { get; init; } = new();

    public List<float> DefaultEmbeddingVector { get; init; } = [0.125f, 0.25f, 0.5f];

    public string DefaultImageDataUri { get; init; } =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGOSHzRgAAAAABJRU5ErkJggg==";

    public byte[] DefaultAudioBytes { get; init; } =
        Convert.FromBase64String("UklGRiQAAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQAAAAA=");

    public string DefaultAudioMediaType { get; init; } = "audio/wav";

    public string DefaultTranscriptionText { get; init; } = "transcribed audio fixture";

    /// <summary>
    ///     Provider-reported usage returned for deterministic transcriptions, or <see langword="null"/>
    ///     to model providers that omit transcription usage.
    /// </summary>
    public LlmTckTranscriptionUsage? DefaultTranscriptionUsage { get; init; } = new();

    public string DefaultTranslationText { get; init; } = "translated audio fixture";

    public byte[] DefaultVideoBytes { get; init; } =
    [
        0x00,
        0x00,
        0x00,
        0x18,
        0x66,
        0x74,
        0x79,
        0x70,
        0x69,
        0x73,
        0x6F,
        0x6D,
        0x00,
        0x00,
        0x02,
        0x00,
        0x69,
        0x73,
        0x6F,
        0x6D,
        0x69,
        0x73,
        0x6F,
        0x32,
    ];

    public string DefaultVideoMediaType { get; init; } = "video/mp4";

    public string DefaultVideoId { get; init; } = "video_llm_tck";

    public string DefaultVideoGenerationId { get; init; } = "gen_llm_tck";

    public long DefaultVideoCreatedAtUnixTime { get; init; } = 1712697600;

    public string DefaultVideoSize { get; init; } = "1280x720";

    public string DefaultVideoSeconds { get; init; } = "4";

    public static LlmTckConfiguration CreateDefault()
    {
        return new()
        {
            Models =
            [
                new LlmTckModel { Id = LlmTckKnownModelIds.Gpt41Mini, Kind = LlmTckModelKind.Chat },
                new LlmTckModel { Id = LlmTckKnownModelIds.TextEmbedding3Small, Kind = LlmTckModelKind.Embedding },
                new LlmTckModel { Id = LlmTckKnownModelIds.GptImage1, Kind = LlmTckModelKind.Image },
                new LlmTckModel { Id = LlmTckKnownModelIds.Gpt4OMiniTts, Kind = LlmTckModelKind.Audio },
                new LlmTckModel { Id = LlmTckKnownModelIds.Sora2, Kind = LlmTckModelKind.Video },
            ],
        };
    }
}

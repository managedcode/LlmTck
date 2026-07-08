using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Runtime;

public interface ILlmTckRuntime
{
    Task ConfigureAsync(LlmTckConfiguration configuration, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);

    bool IsBearerTokenAccepted(string? bearerToken);

    IReadOnlyList<LlmTckModel> GetModels();

    Task<LlmTckChatResult> CompleteChatAsync(
        LlmTckChatRequest request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckEmbeddingResult> CreateEmbeddingAsync(
        string modelId,
        IReadOnlyList<string> inputs,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckImageResult> GenerateImageAsync(
        string modelId,
        string prompt,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckAudioResult> GenerateAudioAsync(
        string modelId,
        string input,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckTranscriptionResult> TranscribeAudioAsync(
        string modelId,
        string fileName,
        string? prompt = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckTranscriptionResult> TranslateAudioAsync(
        string modelId,
        string fileName,
        string? prompt = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    Task<LlmTckVideoResult> GenerateVideoAsync(
        string modelId,
        string prompt,
        string? bearerToken = null,
        CancellationToken cancellationToken = default
    );

    LlmTckAssertionSummary GetAssertionSummary();
}

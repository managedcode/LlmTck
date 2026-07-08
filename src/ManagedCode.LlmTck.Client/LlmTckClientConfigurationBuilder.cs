using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckClientConfigurationBuilder
{
    private readonly LlmTckConfigurationBuilder _builder = new();

    public LlmTckClientConfigurationBuilder RequireBearerToken(string token)
    {
        _builder.RequireBearerToken(token);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseModel(string id, LlmTckModelKind kind)
    {
        _builder.AddModel(id, kind);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseChatModel(string id)
    {
        return UseModel(id, LlmTckModelKind.Chat);
    }

    public LlmTckClientConfigurationBuilder UseEmbeddingModel(string id)
    {
        return UseModel(id, LlmTckModelKind.Embedding);
    }

    public LlmTckClientConfigurationBuilder UseImageModel(string id)
    {
        return UseModel(id, LlmTckModelKind.Image);
    }

    public LlmTckClientConfigurationBuilder UseAudioModel(string id)
    {
        return UseModel(id, LlmTckModelKind.Audio);
    }

    public LlmTckClientConfigurationBuilder UseVideoModel(string id)
    {
        return UseModel(id, LlmTckModelKind.Video);
    }

    public LlmTckClientConfigurationBuilder UseChatScenario(
        string id,
        Action<LlmTckScenarioBuilder> configure
    )
    {
        _builder.AddChatScenario(id, configure);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseDataset(
        string id,
        Action<LlmTckScenarioDatasetBuilder> configure
    )
    {
        _builder.AddDataset(id, configure);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseEmbeddingVector(params float[] values)
    {
        _builder.WithDefaultEmbeddingVector(values);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseImageDataUri(string dataUri)
    {
        _builder.WithDefaultImageDataUri(dataUri);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseAudio(byte[] bytes, string mediaType)
    {
        _builder.WithDefaultAudio(bytes, mediaType);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseVideo(byte[] bytes, string mediaType)
    {
        _builder.WithDefaultVideo(bytes, mediaType);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseTranscriptionText(string text)
    {
        _builder.WithDefaultTranscriptionText(text);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseTranslationText(string text)
    {
        _builder.WithDefaultTranslationText(text);
        return this;
    }

    public LlmTckConfiguration Build()
    {
        return _builder.Build();
    }
}

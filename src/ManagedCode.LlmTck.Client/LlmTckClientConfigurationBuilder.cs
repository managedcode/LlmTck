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

    public LlmTckClientConfigurationBuilder SimulateRateLimitAfter(int allowedRequests)
    {
        _builder.SimulateRateLimitAfter(allowedRequests);
        return this;
    }

    public LlmTckClientConfigurationBuilder SimulateContentFilter(params string[] blockedTerms)
    {
        _builder.SimulateContentFilter(blockedTerms);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseModel(
        string id,
        LlmTckModelKind kind,
        int reasoningTokens = 0
    )
    {
        _builder.AddModel(id, kind, reasoningTokens);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseReasoningChatModel(
        string id,
        int reasoningTokens
    )
    {
        _builder.AddReasoningChatModel(id, reasoningTokens);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseGpt55(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt55, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt55Pro(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt55Pro, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt54(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt54, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt54Pro(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt54Pro, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt54Mini(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt54Mini, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt54Nano(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt54Nano, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt5(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt5, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt5Pro(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt5Pro, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt5Mini(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt5Mini, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt5Nano(int reasoningTokens = 0)
    {
        return UseModel(LlmTckKnownModelIds.Gpt5Nano, LlmTckModelKind.Chat, reasoningTokens);
    }

    public LlmTckClientConfigurationBuilder UseGpt41()
    {
        return UseChatModel(LlmTckKnownModelIds.Gpt41);
    }

    public LlmTckClientConfigurationBuilder UseGpt41Mini()
    {
        return UseChatModel(LlmTckKnownModelIds.Gpt41Mini);
    }

    public LlmTckClientConfigurationBuilder UseGpt4OMini()
    {
        return UseChatModel(LlmTckKnownModelIds.Gpt4OMini);
    }

    public LlmTckClientConfigurationBuilder UseTextEmbedding3Small()
    {
        return UseEmbeddingModel(LlmTckKnownModelIds.TextEmbedding3Small);
    }

    public LlmTckClientConfigurationBuilder UseTextEmbedding3Large()
    {
        return UseEmbeddingModel(LlmTckKnownModelIds.TextEmbedding3Large);
    }

    public LlmTckClientConfigurationBuilder UseTextEmbeddingAda002()
    {
        return UseEmbeddingModel(LlmTckKnownModelIds.TextEmbeddingAda002);
    }

    public LlmTckClientConfigurationBuilder UseGptImage2()
    {
        return UseImageModel(LlmTckKnownModelIds.GptImage2);
    }

    public LlmTckClientConfigurationBuilder UseGptImage15()
    {
        return UseImageModel(LlmTckKnownModelIds.GptImage15);
    }

    public LlmTckClientConfigurationBuilder UseGptImage1()
    {
        return UseImageModel(LlmTckKnownModelIds.GptImage1);
    }

    public LlmTckClientConfigurationBuilder UseGptImage1Mini()
    {
        return UseImageModel(LlmTckKnownModelIds.GptImage1Mini);
    }

    public LlmTckClientConfigurationBuilder UseGpt4OMiniTts()
    {
        return UseAudioModel(LlmTckKnownModelIds.Gpt4OMiniTts);
    }

    public LlmTckClientConfigurationBuilder UseTts1()
    {
        return UseAudioModel(LlmTckKnownModelIds.Tts1);
    }

    public LlmTckClientConfigurationBuilder UseTts1Hd()
    {
        return UseAudioModel(LlmTckKnownModelIds.Tts1Hd);
    }

    public LlmTckClientConfigurationBuilder UseSora2()
    {
        return UseVideoModel(LlmTckKnownModelIds.Sora2);
    }

    public LlmTckClientConfigurationBuilder UseSora2Pro()
    {
        return UseVideoModel(LlmTckKnownModelIds.Sora2Pro);
    }

    public LlmTckClientConfigurationBuilder UseDefaultOpenAiModels()
    {
        _builder.AddDefaultOpenAiModels();
        return this;
    }

    public LlmTckClientConfigurationBuilder UseCurrentOpenAiChatModels(int reasoningTokens = 0)
    {
        _builder.AddCurrentOpenAiChatModels(reasoningTokens);
        return this;
    }

    public LlmTckClientConfigurationBuilder UseOpenAiEmbeddingModels()
    {
        _builder.AddOpenAiEmbeddingModels();
        return this;
    }

    public LlmTckClientConfigurationBuilder UseOpenAiImageModels()
    {
        _builder.AddOpenAiImageModels();
        return this;
    }

    public LlmTckClientConfigurationBuilder UseOpenAiAudioModels()
    {
        _builder.AddOpenAiAudioModels();
        return this;
    }

    public LlmTckClientConfigurationBuilder UseOpenAiVideoModels()
    {
        _builder.AddOpenAiVideoModels();
        return this;
    }

    public LlmTckClientConfigurationBuilder UseKnownOpenAiModels(int reasoningTokens = 0)
    {
        _builder.AddKnownOpenAiModels(reasoningTokens);
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

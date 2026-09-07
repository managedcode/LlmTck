using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Hosting;

public static partial class LlmTckEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private static readonly byte[] _defaultJpegBytes = [0xFF, 0xD8, 0xFF, 0xD9];
    private static readonly string[] _openAiSpeechResponseFormats =
    [
        "mp3",
        "opus",
        "aac",
        "flac",
        "wav",
        "pcm",
    ];
    private static readonly string[] _groqSpeechResponseFormats =
    [
        "flac",
        "mp3",
        "mulaw",
        "ogg",
        "wav",
    ];
    private static readonly string[] _openAiTranscriptionResponseFormats =
    [
        "json",
        "text",
        "srt",
        "verbose_json",
        "vtt",
        "diarized_json",
    ];
    private static readonly string[] _openAiTranslationResponseFormats =
    [
        "json",
        "text",
        "srt",
        "verbose_json",
        "vtt",
    ];
    private static readonly string[] _groqAudioResponseFormats = ["json", "text", "verbose_json"];
    private static readonly string[] _openAiVideoSeconds = ["4", "8", "12"];
    private static readonly string[] _openAiVideoExtensionSeconds = ["4", "8", "12", "16", "20"];
    private static readonly string[] _openAiVideoSizes =
    [
        "720x1280",
        "1280x720",
        "1024x1792",
        "1792x1024",
    ];
    private static readonly string[] _openAiImageResponseFormats = ["url", "b64_json"];
    private static readonly string[] _openAiImageOutputFormats = ["png", "jpeg", "webp"];
    private static readonly string[] _openAiImageQualities =
    [
        "standard",
        "hd",
        "low",
        "medium",
        "high",
        "auto",
    ];
    private static readonly string[] _openAiImageBackgrounds = ["transparent", "opaque", "auto"];
    private static readonly string[] _openAiImageSizes =
    [
        "auto",
        "1024x1024",
        "1536x1024",
        "1024x1536",
        "256x256",
        "512x512",
        "1792x1024",
        "1024x1792",
    ];
    private static readonly string[] _openAiImageVariationSizes =
    [
        "256x256",
        "512x512",
        "1024x1024",
    ];
    private static readonly string[] _providerRouteNamespaces =
    [
        ProviderRoutes.OpenAI,
        ProviderRoutes.AzureOpenAI,
        ProviderRoutes.MicrosoftFoundry,
        ProviderRoutes.Anthropic,
        ProviderRoutes.Gemini,
        ProviderRoutes.Groq,
        ProviderRoutes.Mistral,
        ProviderRoutes.Ollama,
        ProviderRoutes.Cohere,
        ProviderRoutes.Bedrock,
        ProviderRoutes.OpenRouter,
        ProviderRoutes.DeepSeek,
        ProviderRoutes.Perplexity,
    ];

    public static IServiceCollection AddLlmTck(
        this IServiceCollection services,
        Action<LlmTckConfigurationBuilder>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new LlmTckConfigurationBuilder();
        configure?.Invoke(builder);
        var configuration = builder.Build();

        services.AddSingleton<ILlmTckRuntime>(_ => new LlmTckRuntime(configuration));
        services.AddLlmTckProviderHttpTracing();
        services.AddHttpClient();
        services.AddFluentUIComponents();
        services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        return services;
    }

    public static IEndpointRouteBuilder MapLlmTck(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapStaticAssetsIfAvailable(endpoints);
        endpoints
            .MapRazorComponents<LlmTckAdminPage>()
            .AddInteractiveServerRenderMode()
            .DisableAntiforgery();
        endpoints.MapGet(
            LlmTckControlRoutes.Models,
            (HttpContext context, ILlmTckRuntime runtime) =>
                AuthorizeControlRequest(context, runtime) ?? Results.Json(runtime.GetModels())
        );
        endpoints.MapGet(
            LlmTckControlRoutes.Assertions,
            (HttpContext context, ILlmTckRuntime runtime) =>
                AuthorizeControlRequest(context, runtime) ?? Results.Json(runtime.GetAssertionSummary())
        );
        endpoints.MapPost(
            LlmTckControlRoutes.Reset,
            async (
                HttpContext context,
                ILlmTckRuntime runtime,
                ILlmTckProviderHttpTraceStore traceStore,
                CancellationToken cancellationToken
            ) =>
            {
                var unauthorized = AuthorizeControlRequest(context, runtime);
                if (unauthorized is not null)
                {
                    return unauthorized;
                }

                cancellationToken.ThrowIfCancellationRequested();
                traceStore.Reset();
                await runtime.ResetAsync(CancellationToken.None).ConfigureAwait(false);
                return Results.Ok(new { status = "reset" });
            }
        );
        endpoints.MapPost(LlmTckControlRoutes.Configure, ConfigureAsync);

        var providerEndpoints = endpoints.MapGroup(string.Empty);
        providerEndpoints.AddLlmTckProviderHttpTracing();
        providerEndpoints.AddEndpointFilter(async (invocation, next) =>
        {
            var error = LlmTckRequestPolicy.ValidateApiVersion(invocation.HttpContext);
            return error ?? await next(invocation).ConfigureAwait(false);
        });

        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/chat/completions"),
                CompleteOpenAiChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.AzureOpenAI.V1ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/responses"),
                CreateOpenAiResponseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.AzureOpenAI.V1ResponsesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/embeddings"),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.AzureOpenAI.V1EmbeddingsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/openai/v1/chat/completions"),
                CompleteOpenAiChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.MicrosoftFoundry.V1ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/openai/v1/responses"),
                CreateOpenAiResponseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.MicrosoftFoundry.V1ResponsesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/openai/v1/embeddings"),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.MicrosoftFoundry.V1EmbeddingsCreate);

        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/models"),
                (ILlmTckRuntime runtime) =>
                    Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ModelsList);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/models"),
                (ILlmTckRuntime runtime) =>
                    Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.ModelsList);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/models"),
                (ILlmTckRuntime runtime) =>
                    Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenRouter.ModelsList);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.DeepSeek, "/models"),
                (ILlmTckRuntime runtime) =>
                    Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.DeepSeek.ModelsList);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/chat/completions"),
                CompleteOpenAiChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Anthropic, "/v1/messages"),
                CompleteAnthropicMessageAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Anthropic.MessagesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Perplexity, "/v1/sonar"),
                CompleteChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Perplexity.SonarCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/responses"),
                CreateOpenAiResponseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ResponsesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/chat/completions"),
                CompleteGroqChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/responses"),
                CreateGroqResponseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.ResponsesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/speech"),
                GenerateGroqAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.AudioSpeechCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/transcriptions"),
                TranscribeGroqAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.AudioTranscriptionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/translations"),
                TranslateGroqAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Groq.AudioTranslationsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/chat/completions"),
                CompleteOpenRouterChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenRouter.ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/responses"),
                CreateOpenRouterResponseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenRouter.ResponsesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Mistral, "/v1/chat/completions"),
                CompleteMistralChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Mistral.ChatComplete);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Mistral, "/v1/embeddings"),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Mistral.EmbeddingsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.DeepSeek, "/v1/chat/completions"),
                CompleteDeepSeekChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.DeepSeek.ChatCompletionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/embeddings"),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.EmbeddingsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/generations"),
                GenerateImageAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ImagesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/edits"),
                EditImageAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ImagesEditsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/variations"),
                CreateImageVariationAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.ImagesVariationsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/speech"),
                GenerateAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.AudioSpeechCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/transcriptions"),
                TranscribeOpenAiAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.AudioTranscriptionsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/translations"),
                TranslateOpenAiAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.AudioTranslationsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos"),
                CreateOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosCreate);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos"),
                ListOpenAiVideosAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosList);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/characters"),
                CreateOpenAiVideoCharacterAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosCharactersCreate);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.OpenAI,
                    "/v1/videos/characters/{characterId}"
                ),
                GetOpenAiVideoCharacterAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosCharactersRetrieve);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/edits"),
                EditOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosEditsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/extensions"),
                ExtendOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosExtensionsCreate);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}"),
                GetOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosRetrieve);
        providerEndpoints
            .MapDelete(
                ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}"),
                DeleteOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosDelete);
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.OpenAI,
                    "/v1/videos/{videoId}/content"
                ),
                GetOpenAiVideoContentAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosContentRetrieve);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.OpenAI,
                    "/v1/videos/{videoId}/remix"
                ),
                RemixOpenAiVideoAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.VideosRemix);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Ollama, "/api/chat"),
                CompleteOllamaChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Ollama.ChatCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Ollama, "/api/embed"),
                CreateOllamaEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Ollama.EmbeddingsCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Cohere, "/v2/chat"),
                CompleteCohereChatAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Cohere.ChatCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Cohere, "/v2/embed"),
                CreateCohereEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Cohere.EmbedCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Gemini,
                    "/v1beta/models/{model}:generateContent"
                ),
                CompleteGeminiContentAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Gemini.ModelsGenerateContent);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Gemini,
                    "/v1beta/models/{model}:streamGenerateContent"
                ),
                StreamGeminiContentAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.Gemini.ModelsStreamGenerateContent
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Gemini,
                    "/v1beta/models/{model}:embedContent"
                ),
                CreateGeminiEmbeddingAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Gemini.ModelsEmbedContent);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Gemini,
                    "/v1beta/models/{model}:predictLongRunning"
                ),
                StartGeminiVideoOperationAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.Gemini.ModelsPredictLongRunningVideo
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Gemini,
                    "/v1beta/models/{model}/operations/{operationId}"
                ),
                GetGeminiVideoOperationAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.Gemini.ModelsOperationsGetVideo
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/files/{fileId}"),
                GetGeminiFileAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Gemini.FilesGetGeneratedVideo);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/converse"),
                CompleteBedrockConverseAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Bedrock.Converse);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Bedrock,
                    "/model/{modelId}/converse-stream"
                ),
                CompleteBedrockConverseStreamAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Bedrock.ConverseStream);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/invoke"),
                InvokeBedrockModelAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Bedrock.InvokeModel);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.Bedrock,
                    "/model/{modelId}/invoke-with-response-stream"
                ),
                InvokeBedrockModelWithResponseStreamAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.Bedrock.InvokeModelWithResponseStream
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/chat/completions"),
                CompleteFoundryChatAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.MicrosoftFoundry.ChatCompletionsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/embeddings"),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.MicrosoftFoundry.EmbeddingsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.MicrosoftFoundry,
                    "/models/chat/completions"
                ),
                CompleteFoundryChatAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.MicrosoftFoundry.ModelsChatCompletionsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.MicrosoftFoundry,
                    "/models/embeddings"
                ),
                CreateEmbeddingAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.MicrosoftFoundry.ModelsEmbeddingsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/chat/completions"
                ),
                CompleteAzureOpenAiChatAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.ChatCompletionsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/embeddings"
                ),
                CreateAzureOpenAiEmbeddingAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.EmbeddingsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/images/generations"
                ),
                GenerateAzureOpenAiImageAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.AzureOpenAI.ImagesCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/audio/speech"
                ),
                GenerateAzureOpenAiAudioAsync
            )
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.AzureOpenAI.AudioSpeechCreate);
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/audio/transcriptions"
                ),
                TranscribeAzureOpenAiAudioAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.AudioTranscriptionsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/deployments/{deployment}/audio/translations"
                ),
                TranslateAzureOpenAiAudioAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.AudioTranslationsCreate
            );
        providerEndpoints
            .MapPost(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/jobs"
                ),
                CreateAzureOpenAiVideoJobAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsCreate
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/jobs"
                ),
                ListAzureOpenAiVideoJobsAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsList
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/jobs/{jobId}"
                ),
                GetAzureOpenAiVideoJobAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsRetrieve
            );
        providerEndpoints
            .MapDelete(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/jobs/{jobId}"
                ),
                DeleteAzureOpenAiVideoJobAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsDelete
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/{generationId}"
                ),
                GetAzureOpenAiVideoGenerationAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsRetrieve
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/{generationId}/content/thumbnail"
                ),
                GetAzureOpenAiVideoThumbnailAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsThumbnailRetrieve
            );
        providerEndpoints
            .MapGet(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/{generationId}/content/video"
                ),
                GetAzureOpenAiVideoContentAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentRetrieve
            );
        providerEndpoints
            .MapMethods(
                ProviderRoutes.ForProvider(
                    ProviderRoutes.AzureOpenAI,
                    "/openai/v1/video/generations/{generationId}/content/video"
                ),
                ["HEAD"],
                HeadAzureOpenAiVideoContentAsync
            )
            .WithLlmTckProviderOperation(
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentHead
            );

        MapProviderFallbacks(providerEndpoints);

        return endpoints;
    }

    private static void MapProviderFallbacks(RouteGroupBuilder providerEndpoints)
    {
        foreach (var providerNamespace in _providerRouteNamespaces)
        {
            providerEndpoints
                .MapFallback(
                    $"{providerNamespace}/{{**unmatchedProviderPath}}",
                    HandleUnmatchedProviderRequestAsync
                )
                .WithMetadata(new LlmTckProviderFallbackMetadata());
        }
    }

    private static async Task<IResult> HandleUnmatchedProviderRequestAsync(
        HttpContext context,
        EndpointDataSource endpointDataSource,
        IOptions<LlmTckProviderHttpTraceOptions> traceOptions,
        CancellationToken cancellationToken
    )
    {
        await ReadProviderFallbackPreviewAsync(
                context.Request,
                traceOptions.Value.MaxPreviewBytes,
                cancellationToken
            )
            .ConfigureAwait(false);

        var allowedMethods = endpointDataSource
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<LlmTckProviderOperationMetadata>() is not null
                && RouteMatchesPath(endpoint, context.Request.Path)
            )
            .SelectMany(endpoint =>
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedMethods.Length == 0)
        {
            return Results.NotFound();
        }

        context.Response.Headers.Allow = allowedMethods;
        return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    private static bool RouteMatchesPath(RouteEndpoint endpoint, PathString path)
    {
        var rawPattern = endpoint.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            return false;
        }

        var matcher = new TemplateMatcher(
            TemplateParser.Parse(rawPattern.TrimStart('/')),
            new RouteValueDictionary(endpoint.RoutePattern.Defaults)
        );
        return matcher.TryMatch(path, new RouteValueDictionary());
    }

    private static async Task ReadProviderFallbackPreviewAsync(
        HttpRequest request,
        int maxPreviewBytes,
        CancellationToken cancellationToken
    )
    {
        var remaining = maxPreviewBytes + 1;
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Min(remaining, 81920));
        try
        {
            while (remaining > 0)
            {
                var read = await request.Body
                    .ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<IResult> ConfigureAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        ILlmTckProviderHttpTraceStore traceStore,
        CancellationToken cancellationToken
    )
    {
        var unauthorized = AuthorizeControlRequest(context, runtime);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var read = await ReadJsonAsync<LlmTckConfiguration>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return Results.BadRequest(OpenAiWireMapper.ToError("invalid_request", "Missing configuration body."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await runtime.ConfigureAsync(read.Value, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return InvalidRequest("Invalid runtime configuration.");
        }

        traceStore.Reset();
        return Results.Ok(new { status = "configured" });
    }

    private static async Task<IResult> CompleteChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.None,
                OpenAiCacheUsageShape.None,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteOpenAiChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteGroqChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteMistralChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.Mistral,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteOpenRouterChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.OpenRouter,
                OpenAiCacheUsageShape.PromptTokensDetailsWithCacheWriteTokens,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteDeepSeekChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.DeepSeek,
                OpenAiCacheUsageShape.DeepSeekPromptCache,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteFoundryChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                null,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteAzureOpenAiChatAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(
                context,
                runtime,
                deployment,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateOpenAiResponseAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CreateResponseCoreAsync(
                context,
                runtime,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateGroqResponseAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CreateResponseCoreAsync(
                context,
                runtime,
                LlmTckPromptCachePolicy.OpenAiCompatible,
                OpenAiCacheUsageShape.PromptTokensDetails,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateOpenRouterResponseAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CreateResponseCoreAsync(
                context,
                runtime,
                LlmTckPromptCachePolicy.OpenRouter,
                OpenAiCacheUsageShape.PromptTokensDetailsWithCacheWriteTokens,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateResponseCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        LlmTckPromptCachePolicy promptCachePolicy,
        OpenAiCacheUsageShape cacheUsageShape,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiResponseRequest>(context, cancellationToken, validation: OpenAiRequestValidation.ValidateResponse)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing response body.");
        }

        var request = read.Value;
        if (promptCachePolicy == LlmTckPromptCachePolicy.OpenRouter
            && (request.Store == true || request.PreviousResponseId is not null))
        {
            return InvalidRequest("OpenRouter Responses is stateless: store=true and previous_response_id are not supported.");
        }

        if (promptCachePolicy == LlmTckPromptCachePolicy.OpenRouter
            && string.IsNullOrWhiteSpace(request.SessionId)
            && context.Request.Headers.TryGetValue("x-session-id", out var sessionId)
            && !string.IsNullOrWhiteSpace(sessionId.ToString()))
        {
            request = request with { SessionId = sessionId.ToString() };
        }

        var validationError = ValidateResponseRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    OpenAiWireMapper.ToRuntimeRequest(request, promptCachePolicy)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        var responseId = $"resp_{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (request.Stream)
        {
            await WriteStreamingResponseAsync(
                    context,
                    result,
                    responseId,
                    created,
                    cacheUsageShape,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(OpenAiWireMapper.ToResponse(result, responseId, created, cacheUsageShape));
    }

    private static async Task<IResult> CompleteAnthropicMessageAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var versionError = ValidateAnthropicVersion(context);
        if (versionError is not null)
        {
            return versionError;
        }

        var read = await ReadJsonAsync<AnthropicMessagesRequest>(
                context,
                AnthropicInvalidRequest,
                cancellationToken, validation: AnthropicRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return AnthropicInvalidRequest("Missing message body.");
        }

        var validationError = ValidateAnthropicMessageRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(context, AnthropicWireMapper.ToRuntimeRequest(read.Value)),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                AnthropicWireMapper.ToError(
                    ToAnthropicErrorType(result.StatusCode, result.ErrorCode!),
                    result.ErrorMessage!
                ),
                statusCode: result.StatusCode
            );
        }

        if (read.Value.Stream)
        {
            await WriteAnthropicStreamingMessageAsync(context, result, cancellationToken, read.Value.MaxTokens == 0)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(AnthropicWireMapper.ToMessageResponse(result, read.Value.MaxTokens == 0));
    }

    private static async Task<IResult> CompleteOllamaChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OllamaChatRequest>(
                context,
                OllamaInvalidRequest,
                cancellationToken, validation: OllamaRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return OllamaInvalidRequest("Missing chat body.");
        }

        var validationError = ValidateOllamaChatRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(context, OllamaWireMapper.ToRuntimeRequest(read.Value)),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OllamaWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (read.Value.Stream != false)
        {
            await WriteOllamaStreamingChatAsync(context, result, cancellationToken)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(OllamaWireMapper.ToChatResponse(result));
    }

    private static async Task<IResult> CompleteCohereChatAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<CohereChatRequest>(
                context,
                CohereInvalidRequest,
                cancellationToken, validation: CohereRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return CohereInvalidRequest("Missing chat body.");
        }

        var validationError = ValidateCohereChatRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(context, CohereWireMapper.ToRuntimeRequest(read.Value)),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                CohereWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (read.Value.Stream)
        {
            await WriteCohereStreamingChatAsync(context, result, cancellationToken)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(CohereWireMapper.ToChatResponse(result));
    }

    private static async Task<IResult> CompleteGeminiContentAsync(
        string model,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteGeminiContentCoreAsync(
                model,
                context,
                runtime,
                stream: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> StreamGeminiContentAsync(
        string model,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteGeminiContentCoreAsync(
                model,
                context,
                runtime,
                stream: true,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteGeminiContentCoreAsync(
        string model,
        HttpContext context,
        ILlmTckRuntime runtime,
        bool stream,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<GeminiGenerateContentRequest>(
                context,
                GeminiInvalidRequest,
                cancellationToken, validation: GeminiRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return GeminiInvalidRequest("Missing generateContent body.");
        }

        var validationError = ValidateGeminiGenerateContentRequest(model, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    GeminiWireMapper.ToRuntimeRequest(model, read.Value, stream)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                GeminiWireMapper.ToError(result.StatusCode, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (stream)
        {
            await WriteGeminiStreamingContentAsync(context, result, cancellationToken)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(GeminiWireMapper.ToGenerateContentResponse(result, result.Content));
    }

    private static async Task<IResult> CompleteBedrockConverseAsync(
        string modelId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<BedrockConverseRequest>(
                context,
                BedrockInvalidRequest,
                cancellationToken, validation: BedrockRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return BedrockInvalidRequest("Missing converse body.");
        }

        var validationError = ValidateBedrockConverseRequest(modelId, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: false)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(BedrockWireMapper.ToConverseResponse(result))
            : Results.Json(
                BedrockWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> CompleteBedrockConverseStreamAsync(
        string modelId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<BedrockConverseRequest>(
                context,
                BedrockInvalidRequest,
                cancellationToken, validation: BedrockRequestValidation.Validate
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return BedrockInvalidRequest("Missing converse stream body.");
        }

        var validationError = ValidateBedrockConverseRequest(modelId, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: true)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                BedrockWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        await WriteBedrockConverseStreamAsync(context, result, cancellationToken)
            .ConfigureAwait(false);
        return Results.Empty;
    }

    private static async Task<IResult> InvokeBedrockModelAsync(
        string modelId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<JsonElement>(
                context,
                BedrockInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BedrockInvalidRequest("Missing invoke body.");
        }

        var validationError = ValidateBedrockInvokeRequest(modelId, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var input = BedrockWireMapper.ReadInvokeInput(read.Value);
        var modelKind = runtime
            .GetModels()
            .FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase))
            ?.Kind;

        if (modelKind == LlmTckModelKind.Embedding)
        {
            var embedding = await runtime
                .CreateEmbeddingAsync(modelId, [input], ReadAccessToken(context), cancellationToken)
                .ConfigureAwait(false);

            return embedding.IsSuccess
                ? Results.Json(
                    BedrockWireMapper.ToTitanEmbeddingResponse(embedding.Vectors[0], input)
                )
                : Results.Json(
                    BedrockWireMapper.ToError(embedding.ErrorMessage!),
                    statusCode: embedding.StatusCode
                );
        }

        if (modelKind == LlmTckModelKind.Image)
        {
            var image = await runtime
                .GenerateImageAsync(modelId, input, ReadAccessToken(context), cancellationToken)
                .ConfigureAwait(false);

            return image.IsSuccess
                ? Results.Json(BedrockWireMapper.ToImageResponse(image.DataUri))
                : Results.Json(
                    BedrockWireMapper.ToError(image.ErrorMessage!),
                    statusCode: image.StatusCode
                );
        }

        var chat = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    BedrockWireMapper.ToRuntimeRequest(modelId, read.Value)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return chat.IsSuccess
            ? Results.Json(BedrockWireMapper.ToTitanTextResponse(chat, input))
            : Results.Json(
                BedrockWireMapper.ToError(chat.ErrorMessage!),
                statusCode: chat.StatusCode
            );
    }

    private static async Task<IResult> InvokeBedrockModelWithResponseStreamAsync(
        string modelId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<JsonElement>(
                context,
                BedrockInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BedrockInvalidRequest("Missing invoke stream body.");
        }

        var validationError = ValidateBedrockInvokeRequest(modelId, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: true)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                BedrockWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        await WriteBedrockInvokeModelStreamAsync(context, result, cancellationToken)
            .ConfigureAwait(false);
        return Results.Empty;
    }

    private static async Task<IResult> CompleteChatCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        LlmTckPromptCachePolicy promptCachePolicy,
        OpenAiCacheUsageShape cacheUsageShape,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiChatCompletionRequest>(context, cancellationToken, validation: OpenAiRequestValidation.ValidateChat)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing chat completion body.");
        }

        var request = string.IsNullOrWhiteSpace(modelOverride)
            ? read.Value
            : read.Value with { Model = modelOverride };
        if (promptCachePolicy == LlmTckPromptCachePolicy.OpenRouter
            && string.IsNullOrWhiteSpace(request.SessionId)
            && context.Request.Headers.TryGetValue("x-session-id", out var sessionId)
            && !string.IsNullOrWhiteSpace(sessionId.ToString()))
        {
            request = request with { SessionId = sessionId.ToString() };
        }

        var validationError = ValidateChatRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                CorrelateChatRequest(
                    context,
                    OpenAiWireMapper.ToRuntimeRequest(request, promptCachePolicy)
                ),
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (request.Stream)
        {
            await WriteStreamingChatAsync(context, result, request.StreamOptions?.IncludeUsage == true, cacheUsageShape, cancellationToken).ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(OpenAiWireMapper.ToChatResponse(result, cacheUsageShape));
    }

    private static async Task<IResult> CreateEmbeddingAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CreateEmbeddingCoreAsync(context, runtime, null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateAzureOpenAiEmbeddingAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CreateEmbeddingCoreAsync(context, runtime, deployment, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateEmbeddingCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiEmbeddingRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return Results.BadRequest(OpenAiWireMapper.ToError("invalid_request", "Missing embedding body."));
        }

        var request = string.IsNullOrWhiteSpace(modelOverride)
            ? read.Value
            : read.Value with { Model = modelOverride };
        var validationError = ValidateEmbeddingRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var inputs = ReadEmbeddingInputs(request.Input);
        var result = await runtime
            .CreateEmbeddingAsync(request.Model, inputs, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(
                OpenAiWireMapper.ToEmbeddingResponse(
                    request.Model,
                    result.Vectors,
                    request.UsesBase64Encoding
                )
            )
            : Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> CreateOllamaEmbeddingAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OllamaEmbedRequest>(
                context,
                OllamaInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return OllamaInvalidRequest("Missing embedding body.");
        }

        var validationError = ValidateOllamaEmbeddingRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var inputs = OllamaWireMapper.ReadEmbeddingInputs(read.Value);
        var result = await runtime
            .CreateEmbeddingAsync(
                read.Value.Model,
                inputs,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(OllamaWireMapper.ToEmbedResponse(read.Value.Model, result.Vectors))
            : Results.Json(
                OllamaWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> CreateCohereEmbeddingAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<CohereEmbedRequest>(
                context,
                CohereInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return CohereInvalidRequest("Missing embedding body.");
        }

        var validationError = ValidateCohereEmbeddingRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var inputs = CohereWireMapper.ReadEmbeddingInputs(read.Value);
        var result = await runtime
            .CreateEmbeddingAsync(
                read.Value.Model,
                inputs,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(CohereWireMapper.ToEmbedResponse(result.Vectors))
            : Results.Json(
                CohereWireMapper.ToError(result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> CreateGeminiEmbeddingAsync(
        string model,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<GeminiEmbedContentRequest>(
                context,
                GeminiInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return GeminiInvalidRequest("Missing embedContent body.");
        }

        var validationError = ValidateGeminiEmbeddingRequest(model, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CreateEmbeddingAsync(
                model,
                [GeminiWireMapper.ReadText(read.Value.Content)],
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(GeminiWireMapper.ToEmbedContentResponse(result.Vectors[0]))
            : Results.Json(
                GeminiWireMapper.ToError(result.StatusCode, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> StartGeminiVideoOperationAsync(
        string model,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<GeminiPredictLongRunningRequest>(
                context,
                GeminiInvalidRequest,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return GeminiInvalidRequest("Missing predictLongRunning body.");
        }

        var validationError = ValidateGeminiPredictLongRunningRequest(model, read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var prompt = GeminiWireMapper.ReadPredictPrompt(read.Value);
        var result = await runtime
            .GenerateVideoAsync(model, prompt, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ToGeminiVideoError(result);
        }

        return Results.Json(
            GeminiWireMapper.ToVideoOperationResponse(
                model,
                result,
                CreateGeminiFileUri(context, result.VideoId, media: false),
                done: false
            )
        );
    }

    private static async Task<IResult> GetGeminiVideoOperationAsync(
        string model,
        string operationId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return GeminiInvalidRequest("Missing model path parameter.");
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            return GeminiInvalidRequest("Missing operation id.");
        }

        var result = await runtime
            .GenerateVideoAsync(
                model,
                $"poll {GeminiWireMapper.CreateVideoOperationName(model, operationId)}",
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ToGeminiVideoError(result);
        }

        return Results.Json(
            GeminiWireMapper.ToVideoOperationResponse(
                model,
                result with { GenerationId = operationId },
                CreateGeminiFileUri(context, result.VideoId, media: false),
                done: true
            )
        );
    }

    private static async Task<IResult> GetGeminiFileAsync(
        string fileId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return GeminiInvalidRequest("Missing file id.");
        }

        var modelId = runtime
            .GetModels()
            .FirstOrDefault(model => model.Kind == LlmTckModelKind.Video)
            ?.Id;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.Json(
                GeminiWireMapper.ToError(
                    StatusCodes.Status404NotFound,
                    "No video model is configured."
                ),
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var result = await runtime
            .GenerateVideoAsync(
                modelId,
                $"download {GeminiWireMapper.CreateFileName(fileId)}",
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ToGeminiVideoError(result);
        }

        if (string.Equals(context.Request.Query["alt"].ToString(), "media", StringComparison.Ordinal))
        {
            return Results.File(result.Bytes, result.MediaType, $"{fileId}.mp4");
        }

        return Results.Json(
            GeminiWireMapper.ToFileResponse(
                fileId,
                result,
                CreateGeminiFileUri(context, fileId, media: false),
                CreateGeminiFileUri(context, fileId, media: true)
            )
        );
    }

    private static async Task<IResult> GenerateImageAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateImageCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowStreaming: true,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateAzureOpenAiImageAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateImageCoreAsync(
                context,
                runtime,
                modelOverride: deployment,
                allowStreaming: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateImageCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        bool allowStreaming,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiImageGenerationRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return Results.BadRequest(OpenAiWireMapper.ToError("invalid_request", "Missing image body."));
        }

        var request = string.IsNullOrWhiteSpace(modelOverride)
            ? read.Value
            : read.Value with { Model = modelOverride };
        var validationError = ValidateImageRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        if (request.Stream && !allowStreaming)
        {
            return InvalidRequest("Streaming image generation is not supported for this provider route.");
        }

        var result = await runtime
            .GenerateImageAsync(request.Model, request.Prompt, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (request.Stream)
        {
            await WriteImageStreamingAsync(
                    context,
                    result.DataUri,
                    "image_generation",
                    request.Background,
                    request.OutputFormat,
                    request.Quality,
                    request.Size,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(
            OpenAiWireMapper.ToImageResponse(
                result.DataUri,
                request.Background,
                request.OutputFormat,
                request.Quality,
                request.Size
            )
        );
    }

    private static async Task<IResult> EditImageAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadImageEditRequestAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var request = read.Value!;
        var validationError = ValidateImageEditRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .GenerateImageAsync(request.Model, request.Prompt, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (request.Stream)
        {
            await WriteImageStreamingAsync(
                    context,
                    result.DataUri,
                    "image_edit",
                    request.Background,
                    request.OutputFormat,
                    request.Quality,
                    request.Size,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(
            OpenAiWireMapper.ToImageResponse(
                result.DataUri,
                request.Background,
                request.OutputFormat,
                request.Quality,
                request.Size
            )
        );
    }

    private static async Task<IResult> CreateImageVariationAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadImageVariationRequestAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var request = read.Value!;
        var validationError = ValidateImageVariationRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .GenerateImageAsync(
                request.Model,
                "image variation fixture",
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(OpenAiWireMapper.ToImageResponse(result.DataUri, size: request.Size))
            : Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> GenerateAzureOpenAiAudioAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateAudioCoreAsync(
                context,
                runtime,
                modelOverride: deployment,
                allowedResponseFormats: _openAiSpeechResponseFormats,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranscribeOpenAiAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranscribeAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowUrlInput: false,
                allowStreaming: true,
                allowedResponseFormats: _openAiTranscriptionResponseFormats,
                includeGroqMetadata: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranscribeGroqAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranscribeAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowUrlInput: true,
                allowStreaming: false,
                allowedResponseFormats: _groqAudioResponseFormats,
                includeGroqMetadata: true,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranscribeAzureOpenAiAudioAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranscribeAudioCoreAsync(
                context,
                runtime,
                modelOverride: deployment,
                allowUrlInput: false,
                allowStreaming: false,
                allowedResponseFormats: _openAiTranslationResponseFormats,
                includeGroqMetadata: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranslateOpenAiAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranslateAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowUrlInput: false,
                allowedResponseFormats: _openAiTranslationResponseFormats,
                includeGroqMetadata: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranslateGroqAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranslateAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowUrlInput: true,
                allowedResponseFormats: _groqAudioResponseFormats,
                includeGroqMetadata: true,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> TranslateAzureOpenAiAudioAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await TranslateAudioCoreAsync(
                context,
                runtime,
                modelOverride: deployment,
                allowUrlInput: false,
                allowedResponseFormats: _openAiTranslationResponseFormats,
                includeGroqMetadata: false,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateAudioCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        string[] allowedResponseFormats,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiAudioSpeechRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return Results.BadRequest(OpenAiWireMapper.ToError("invalid_request", "Missing audio body."));
        }

        var request = string.IsNullOrWhiteSpace(modelOverride)
            ? read.Value
            : read.Value with { Model = modelOverride };
        var validationError = ValidateAudioRequest(request, allowedResponseFormats);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .GenerateAudioAsync(request.Model, request.Input, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Bytes(result.Bytes, result.MediaType)
            : Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> TranscribeAudioCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        bool allowUrlInput,
        bool allowStreaming,
        string[] allowedResponseFormats,
        bool includeGroqMetadata,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadAudioFormAsync(
                context,
                modelOverride,
                allowUrlInput,
                allowStreaming,
                allowedResponseFormats,
                "transcription",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var request = read.Value;
        var result = await runtime
            .TranscribeAudioAsync(
                request.Model,
                request.FileName,
                request.Prompt,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        if (request.Stream)
        {
            await WriteStreamingTranscriptionAsync(context, result, cancellationToken)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return request.ResponseFormat switch
        {
            "text" or "srt" or "vtt" => Results.Text(result.Text, "text/plain"),
            "verbose_json" or "diarized_json" => Results.Json(
                OpenAiWireMapper.ToVerboseAudioTranscriptionResponse(result)
            ),
            _ when includeGroqMetadata => Results.Json(
                OpenAiWireMapper.ToGroqAudioTranscriptionResponse(result)
            ),
            _ => Results.Json(OpenAiWireMapper.ToAudioTranscriptionResponse(result)),
        };
    }

    private static async Task<IResult> TranslateAudioCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
        bool allowUrlInput,
        string[] allowedResponseFormats,
        bool includeGroqMetadata,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadAudioFormAsync(
                context,
                modelOverride,
                allowUrlInput,
                allowStreaming: false,
                allowedResponseFormats,
                "translation",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var request = read.Value;
        var result = await runtime
            .TranslateAudioAsync(
                request.Model,
                request.FileName,
                request.Prompt,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
        }

        return request.ResponseFormat switch
        {
            "text" or "srt" or "vtt" => Results.Text(result.Text, "text/plain"),
            "verbose_json" => Results.Json(
                OpenAiWireMapper.ToVerboseAudioTranscriptionResponse(result)
            ),
            _ when includeGroqMetadata => Results.Json(
                OpenAiWireMapper.ToGroqAudioTranscriptionResponse(result)
            ),
            _ => Results.Json(OpenAiWireMapper.ToAudioTranscriptionResponse(result)),
        };
    }

    private static async Task<FormReadResult> ReadAudioFormAsync(
        HttpContext context,
        string? modelOverride,
        bool allowUrlInput,
        bool allowStreaming,
        string[] allowedResponseFormats,
        string operationName,
        CancellationToken cancellationToken
    )
    {
        if (!context.Request.HasFormContentType)
        {
            return new(default, InvalidRequest($"Audio {operationName} requires multipart/form-data."));
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }
        catch (BadHttpRequestException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }

        var model = string.IsNullOrWhiteSpace(modelOverride)
            ? form["model"].ToString()
            : modelOverride;
        if (string.IsNullOrWhiteSpace(model))
        {
            return new(default, InvalidRequest($"Missing {operationName} model."));
        }

        var file = form.Files.GetFile("file");
        var url = form["url"].ToString();
        if (file is null && (!allowUrlInput || string.IsNullOrWhiteSpace(url)))
        {
            return new(default, InvalidRequest($"Missing {operationName} file."));
        }

        var responseFormat = form["response_format"].ToString();
        if (string.IsNullOrWhiteSpace(responseFormat))
        {
            responseFormat = "json";
        }

        if (!IsSupportedAudioResponseFormat(responseFormat, allowedResponseFormats))
        {
            return new(default, InvalidRequest($"Unsupported {operationName} response_format."));
        }

        var stream = bool.TryParse(form["stream"].ToString(), out var parsedStream) && parsedStream;
        if (stream && !allowStreaming)
        {
            return new(default, InvalidRequest($"Streaming {operationName} is not supported for this provider route."));
        }

        return new(
            new AudioFormRequest(
                model,
                file?.FileName ?? url,
                form["prompt"].ToString(),
                responseFormat,
                stream
            ),
            null
        );
    }

    private static async Task<JsonReadResult<OpenAiVideoCreateRequest>> ReadOpenAiVideoCreateRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken
    )
    {
        if (!context.Request.HasFormContentType)
        {
            return await ReadJsonAsync<OpenAiVideoCreateRequest>(context, cancellationToken)
                .ConfigureAwait(false);
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }
        catch (BadHttpRequestException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }

        var model = form["model"].ToString();
        return new(
            new OpenAiVideoCreateRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? LlmTckKnownModelIds.Sora2 : model,
                Prompt = form["prompt"].ToString(),
                Seconds = EmptyToNull(form["seconds"].ToString()),
                Size = EmptyToNull(form["size"].ToString()),
            },
            null
        );
    }

    private static async Task<JsonReadResult<OpenAiImageEditRequest>> ReadImageEditRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken
    )
    {
        if (!context.Request.HasFormContentType)
        {
            return await ReadJsonAsync<OpenAiImageEditRequest>(context, cancellationToken)
                .ConfigureAwait(false);
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }
        catch (BadHttpRequestException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }

        var model = form["model"].ToString();
        return new(
            new OpenAiImageEditRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? LlmTckKnownModelIds.GptImage1 : model,
                Prompt = form["prompt"].ToString(),
                Images = ReadImageReferences(form),
                Stream = bool.TryParse(form["stream"].ToString(), out var stream) && stream,
                PartialImages = TryReadInt(form["partial_images"].ToString()),
                Size = EmptyToNull(form["size"].ToString()),
                Quality = EmptyToNull(form["quality"].ToString()),
                Background = EmptyToNull(form["background"].ToString()),
                OutputFormat = EmptyToNull(form["output_format"].ToString()),
            },
            null
        );
    }

    private static async Task<JsonReadResult<OpenAiImageVariationRequest>> ReadImageVariationRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken
    )
    {
        if (!context.Request.HasFormContentType)
        {
            return new(default, InvalidRequest("Image variations require multipart/form-data."));
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }
        catch (BadHttpRequestException)
        {
            return new(default, InvalidRequest("Malformed multipart form data."));
        }

        if (form.Files.GetFile("image") is null)
        {
            return new(default, InvalidRequest("Image variations require an image file."));
        }

        var model = form["model"].ToString();
        return new(
            new OpenAiImageVariationRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? "dall-e-2" : model,
                Count = TryReadInt(form["n"].ToString()),
                ResponseFormat = EmptyToNull(form["response_format"].ToString()),
                Size = EmptyToNull(form["size"].ToString()),
            },
            null
        );
    }

    private static IReadOnlyList<OpenAiImageReference> ReadImageReferences(IFormCollection form)
    {
        var references = form
            .Files
            .Where(file => file.Name is "image" or "image[]")
            .Select(file => new OpenAiImageReference { FileId = file.FileName })
            .ToList();

        var imageUrl = form["image_url"].ToString();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            references.Add(new OpenAiImageReference { ImageUrl = imageUrl });
        }

        return references;
    }

    private static bool IsSupportedAudioResponseFormat(
        string responseFormat,
        string[] allowedResponseFormats
    )
    {
        return Array.Exists(
            allowedResponseFormats,
            format => string.Equals(format, responseFormat, StringComparison.Ordinal)
        );
    }

    private static IResult? AuthorizeControlRequest(HttpContext context, ILlmTckRuntime runtime)
    {
        return runtime.IsBearerTokenAccepted(ReadAccessToken(context))
            ? null
            : Results.Json(
                OpenAiWireMapper.ToError(
                    "invalid_api_key",
                    "The supplied bearer token did not match the configured LLM TCK token."
                ),
                statusCode: StatusCodes.Status401Unauthorized
            );
    }

    private static void MapStaticAssetsIfAvailable(IEndpointRouteBuilder endpoints)
    {
        var entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrWhiteSpace(entryAssemblyName))
        {
            return;
        }

        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{entryAssemblyName}.staticwebassets.endpoints.json"
        );
        var runtimeManifestPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{entryAssemblyName}.staticwebassets.runtime.json"
        );
        if (File.Exists(manifestPath))
        {
            UseStaticWebAssetsRuntimeManifestIfAvailable(endpoints, runtimeManifestPath);

            endpoints.MapStaticAssets(manifestPath);
        }
    }

    private static void UseStaticWebAssetsRuntimeManifestIfAvailable(
        IEndpointRouteBuilder endpoints,
        string runtimeManifestPath
    )
    {
        if (!File.Exists(runtimeManifestPath))
        {
            return;
        }

        var environment = endpoints.ServiceProvider.GetService<IWebHostEnvironment>();
        var configuration = endpoints.ServiceProvider.GetService<IConfiguration>();
        if (environment is null || configuration is null)
        {
            return;
        }

        try
        {
            StaticWebAssetsLoader.UseStaticWebAssets(environment, configuration);
        }
        catch (DirectoryNotFoundException exception)
        {
            endpoints.ServiceProvider
                .GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(LlmTckEndpointRouteBuilderExtensions))
                .LogWarning(
                    exception,
                    "Skipping LLM TCK static web assets runtime manifest because it references a missing content root."
                );
        }
    }

    private static async Task<JsonReadResult<T>> ReadJsonAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<JsonElement, LlmTckRequestValidationResult>? validation = null
    )
    {
        return await ReadJsonAsync<T>(context, InvalidRequest, cancellationToken, validation)
            .ConfigureAwait(false);
    }

    private static async Task<JsonReadResult<T>> ReadJsonAsync<T>(
        HttpContext context,
        Func<string, IResult> invalidRequest,
        CancellationToken cancellationToken,
        Func<JsonElement, LlmTckRequestValidationResult>? validation = null
    )
    {
        try
        {
            var body = await context.Request.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (LlmTckRequestPolicy.IsChatRequest<T>())
            {
                if (typeof(T) == typeof(JsonElement) && LlmTckRequestPolicy.HasUnsupportedChatFeatures(body))
                {
                    return new JsonReadResult<T>(default, invalidRequest("Tool calls and structured output fixtures are not supported by LlmTck on this operation."));
                }

                if (context.Request.Path.StartsWithSegments("/perplexity") && body.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() > 0)
                {
                    return new JsonReadResult<T>(default, invalidRequest("Function tool fixtures are not supported by this provider."));
                }
            }

            if (validation?.Invoke(body) is { IsValid: false } failure)
            {
                return new JsonReadResult<T>(default, invalidRequest(failure.Error!));
            }

            return new JsonReadResult<T>(body.Deserialize<T>(_jsonOptions), null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new JsonReadResult<T>(default, invalidRequest("Malformed JSON request body."));
        }
        catch (BadHttpRequestException)
        {
            return new JsonReadResult<T>(default, invalidRequest("Malformed JSON request body."));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<LlmTckModelKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<LlmTckMatchMode>(JsonNamingPolicy.CamelCase));
        return options;
    }
}

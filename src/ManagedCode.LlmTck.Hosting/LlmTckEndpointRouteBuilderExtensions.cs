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
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Hosting;

public static class LlmTckEndpointRouteBuilderExtensions
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

    public static IServiceCollection AddLlmTck(
        this IServiceCollection services,
        Action<LlmTckConfigurationBuilder>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new LlmTckConfigurationBuilder();
        configure?.Invoke(builder);
        var configuration = builder.Build();

        services.AddSingleton<ILlmTckRuntime>(_ =>
        {
            var runtime = new LlmTckRuntime();
            runtime.ConfigureAsync(configuration).GetAwaiter().GetResult();
            return runtime;
        });
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
            async (HttpContext context, ILlmTckRuntime runtime, CancellationToken cancellationToken) =>
            {
                var unauthorized = AuthorizeControlRequest(context, runtime);
                if (unauthorized is not null)
                {
                    return unauthorized;
                }

                await runtime.ResetAsync(cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { status = "reset" });
            }
        );
        endpoints.MapPost(LlmTckControlRoutes.Configure, ConfigureAsync);

        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/models"),
            (ILlmTckRuntime runtime) => Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/models"),
            (ILlmTckRuntime runtime) => Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/models"),
            (ILlmTckRuntime runtime) => Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.DeepSeek, "/models"),
            (ILlmTckRuntime runtime) => Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
        );
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/chat/completions"), CompleteOpenAiChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Anthropic, "/v1/messages"), CompleteAnthropicMessageAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Perplexity, "/v1/sonar"), CompleteChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/responses"), CreateOpenAiResponseAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/chat/completions"), CompleteGroqChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/responses"), CreateGroqResponseAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/speech"), GenerateGroqAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/transcriptions"), TranscribeGroqAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Groq, "/openai/v1/audio/translations"), TranslateGroqAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/chat/completions"), CompleteOpenRouterChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenRouter, "/api/v1/responses"), CreateOpenRouterResponseAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Mistral, "/v1/chat/completions"), CompleteMistralChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Mistral, "/v1/embeddings"), CreateEmbeddingAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.DeepSeek, "/v1/chat/completions"), CompleteDeepSeekChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/embeddings"), CreateEmbeddingAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/generations"), GenerateImageAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/edits"), EditImageAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/images/variations"), CreateImageVariationAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/speech"), GenerateAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/transcriptions"), TranscribeOpenAiAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/audio/translations"), TranslateOpenAiAudioAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos"), CreateOpenAiVideoAsync);
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos"), ListOpenAiVideosAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/characters"), CreateOpenAiVideoCharacterAsync);
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/characters/{characterId}"), GetOpenAiVideoCharacterAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/edits"), EditOpenAiVideoAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/extensions"), ExtendOpenAiVideoAsync);
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}"), GetOpenAiVideoAsync);
        endpoints.MapDelete(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}"), DeleteOpenAiVideoAsync);
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}/content"), GetOpenAiVideoContentAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/videos/{videoId}/remix"), RemixOpenAiVideoAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Ollama, "/api/chat"), CompleteOllamaChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Ollama, "/api/embed"), CreateOllamaEmbeddingAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Cohere, "/v2/chat"), CompleteCohereChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Cohere, "/v2/embed"), CreateCohereEmbeddingAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/models/{model}:generateContent"), CompleteGeminiContentAsync);
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/models/{model}:streamGenerateContent"),
            StreamGeminiContentAsync
        );
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/models/{model}:embedContent"), CreateGeminiEmbeddingAsync);
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/models/{model}:predictLongRunning"),
            StartGeminiVideoOperationAsync
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/models/{model}/operations/{operationId}"),
            GetGeminiVideoOperationAsync
        );
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.Gemini, "/v1beta/files/{fileId}"), GetGeminiFileAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/converse"), CompleteBedrockConverseAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/converse-stream"), CompleteBedrockConverseStreamAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/invoke"), InvokeBedrockModelAsync);
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.Bedrock, "/model/{modelId}/invoke-with-response-stream"),
            InvokeBedrockModelWithResponseStreamAsync
        );
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/chat/completions"), CompleteFoundryChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/embeddings"), CreateEmbeddingAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/models/chat/completions"), CompleteFoundryChatAsync);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.MicrosoftFoundry, "/models/embeddings"), CreateEmbeddingAsync);
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/chat/completions"),
            CompleteAzureOpenAiChatAsync
        );
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/embeddings"),
            CreateAzureOpenAiEmbeddingAsync
        );
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/images/generations"),
            GenerateAzureOpenAiImageAsync
        );
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/audio/speech"),
            GenerateAzureOpenAiAudioAsync
        );
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/audio/transcriptions"),
            TranscribeAzureOpenAiAudioAsync
        );
        endpoints.MapPost(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/deployments/{deployment}/audio/translations"),
            TranslateAzureOpenAiAudioAsync
        );
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/jobs"), CreateAzureOpenAiVideoJobAsync);
        endpoints.MapGet(ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/jobs"), ListAzureOpenAiVideoJobsAsync);
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/jobs/{jobId}"),
            GetAzureOpenAiVideoJobAsync
        );
        endpoints.MapDelete(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/jobs/{jobId}"),
            DeleteAzureOpenAiVideoJobAsync
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/{generationId}"),
            GetAzureOpenAiVideoGenerationAsync
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/{generationId}/content/thumbnail"),
            GetAzureOpenAiVideoThumbnailAsync
        );
        endpoints.MapGet(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/{generationId}/content/video"),
            GetAzureOpenAiVideoContentAsync
        );
        endpoints.MapMethods(
            ProviderRoutes.ForProvider(ProviderRoutes.AzureOpenAI, "/openai/v1/video/generations/{generationId}/content/video"),
            ["HEAD"],
            HeadAzureOpenAiVideoContentAsync
        );

        return endpoints;
    }

    private static async Task<IResult> ConfigureAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
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

        await runtime.ConfigureAsync(read.Value, cancellationToken).ConfigureAwait(false);
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
        var read = await ReadJsonAsync<OpenAiResponseRequest>(context, cancellationToken)
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
                OpenAiWireMapper.ToRuntimeRequest(request, promptCachePolicy),
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
                cancellationToken
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
                AnthropicWireMapper.ToRuntimeRequest(read.Value),
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
            await WriteAnthropicStreamingMessageAsync(context, result, cancellationToken)
                .ConfigureAwait(false);
            return Results.Empty;
        }

        return Results.Json(AnthropicWireMapper.ToMessageResponse(result));
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
                cancellationToken
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
                OllamaWireMapper.ToRuntimeRequest(read.Value),
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
                cancellationToken
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
                CohereWireMapper.ToRuntimeRequest(read.Value),
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
                cancellationToken
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
                GeminiWireMapper.ToRuntimeRequest(model, read.Value, stream),
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
                cancellationToken
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
                BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: false),
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
                cancellationToken
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
                BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: true),
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
                BedrockWireMapper.ToRuntimeRequest(modelId, read.Value),
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
                BedrockWireMapper.ToRuntimeRequest(modelId, read.Value, stream: true),
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
        var read = await ReadJsonAsync<OpenAiChatCompletionRequest>(context, cancellationToken)
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
                OpenAiWireMapper.ToRuntimeRequest(request, promptCachePolicy),
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
            await WriteStreamingChatAsync(context, result, cancellationToken).ConfigureAwait(false);
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

    private static async Task<IResult> CreateOpenAiVideoAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadOpenAiVideoCreateRequestAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing video body.");
        }

        var validationError = ValidateOpenAiVideoCreateRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .GenerateVideoAsync(
                read.Value.Model,
                read.Value.Prompt,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ToOpenAiVideoError(result);
        }

        return Results.Json(
            OpenAiWireMapper.ToVideoResponse(
                result with
                {
                    Seconds = read.Value.Seconds ?? result.Seconds,
                    Size = read.Value.Size ?? result.Size,
                }
            )
        );
    }

    private static async Task<IResult> ListOpenAiVideosAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var validationError = ValidateOpenAiVideoListQuery(context);
        if (validationError is not null)
        {
            return validationError;
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "listed video fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error ?? Results.Json(OpenAiWireMapper.ToVideoListResponse(read.Value!));
    }

    private static async Task<IResult> GetOpenAiVideoAsync(
        string videoId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return InvalidRequest("Missing video_id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved video fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error
            ?? Results.Json(OpenAiWireMapper.ToVideoResponse(read.Value! with { VideoId = videoId }));
    }

    private static IResult DeleteOpenAiVideoAsync(
        string videoId,
        HttpContext context,
        ILlmTckRuntime runtime
    )
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return InvalidRequest("Missing video_id path parameter.");
        }

        var unauthorized = AuthorizeControlRequest(context, runtime);
        return unauthorized ?? Results.Json(OpenAiWireMapper.ToVideoDeleteResponse(videoId));
    }

    private static async Task<IResult> GetOpenAiVideoContentAsync(
        string videoId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return InvalidRequest("Missing video_id path parameter.");
        }

        var variant = context.Request.Query["variant"].ToString();
        if (
            !string.IsNullOrWhiteSpace(variant)
            && variant is not ("video" or "thumbnail" or "spritesheet")
        )
        {
            return InvalidRequest("Unsupported video content variant.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "downloaded video fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        return variant is "thumbnail" or "spritesheet"
            ? Results.Bytes(_defaultJpegBytes, "image/jpeg")
            : Results.Bytes(read.Value!.Bytes, read.Value.MediaType);
    }

    private static async Task<IResult> EditOpenAiVideoAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiVideoEditRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing video edit body.");
        }

        if (
            string.IsNullOrWhiteSpace(read.Value.Prompt)
            || string.IsNullOrWhiteSpace(read.Value.Video?.Id)
        )
        {
            return InvalidRequest("Video edits require prompt and video.id.");
        }

        return await GenerateOpenAiVideoFromDefaultModelAsync(
                context,
                runtime,
                read.Value.Prompt,
                seconds: null,
                remixedFromVideoId: read.Value.Video.Id,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ExtendOpenAiVideoAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<OpenAiVideoExtensionRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing video extension body.");
        }

        if (
            string.IsNullOrWhiteSpace(read.Value.Prompt)
            || string.IsNullOrWhiteSpace(read.Value.Video?.Id)
        )
        {
            return InvalidRequest("Video extensions require prompt and video.id.");
        }

        if (!IsAllowedOpenAiVideoExtensionSeconds(read.Value.Seconds))
        {
            return InvalidRequest("Unsupported video extension seconds.");
        }

        return await GenerateOpenAiVideoFromDefaultModelAsync(
                context,
                runtime,
                read.Value.Prompt,
                read.Value.Seconds,
                remixedFromVideoId: read.Value.Video.Id,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> RemixOpenAiVideoAsync(
        string videoId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return InvalidRequest("Missing video_id path parameter.");
        }

        var read = await ReadJsonAsync<OpenAiVideoRemixRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null || string.IsNullOrWhiteSpace(read.Value.Prompt))
        {
            return InvalidRequest("Video remix requires prompt.");
        }

        return await GenerateOpenAiVideoFromDefaultModelAsync(
                context,
                runtime,
                read.Value.Prompt,
                seconds: null,
                remixedFromVideoId: videoId,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CreateOpenAiVideoCharacterAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (!context.Request.HasFormContentType)
        {
            return InvalidRequest("Video character creation requires multipart/form-data.");
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return InvalidRequest("Malformed multipart form data.");
        }
        catch (BadHttpRequestException)
        {
            return InvalidRequest("Malformed multipart form data.");
        }

        var unauthorized = AuthorizeControlRequest(context, runtime);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var name = form["name"].ToString();
        if (string.IsNullOrWhiteSpace(name) || form.Files.GetFile("video") is null)
        {
            return InvalidRequest("Video character creation requires name and video file.");
        }

        return Results.Json(
            OpenAiWireMapper.ToVideoCharacterResponse(
                "char_llm_tck",
                name,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            )
        );
    }

    private static IResult GetOpenAiVideoCharacterAsync(
        string characterId,
        HttpContext context,
        ILlmTckRuntime runtime
    )
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return InvalidRequest("Missing character_id path parameter.");
        }

        var unauthorized = AuthorizeControlRequest(context, runtime);
        return unauthorized
            ?? Results.Json(
                OpenAiWireMapper.ToVideoCharacterResponse(
                    characterId,
                    "LLM TCK Character",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                )
            );
    }

    private static async Task<IResult> GenerateOpenAiVideoFromDefaultModelAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string prompt,
        string? seconds,
        string? remixedFromVideoId,
        CancellationToken cancellationToken
    )
    {
        var read = await GenerateDefaultVideoFixtureAsync(context, runtime, prompt, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var result = read.Value!;
        if (!string.IsNullOrWhiteSpace(seconds))
        {
            result = result with { Seconds = seconds };
        }

        var response = OpenAiWireMapper.ToVideoResponse(result) with
        {
            RemixedFromVideoId = remixedFromVideoId,
        };
        return Results.Json(response);
    }

    private static async Task<IResult> CreateAzureOpenAiVideoJobAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await ReadJsonAsync<AzureVideoGenerationJobRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        if (read.Value is null)
        {
            return InvalidRequest("Missing video generation job body.");
        }

        var validationError = ValidateAzureVideoGenerationJobRequest(read.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .GenerateVideoAsync(
                read.Value.Model,
                read.Value.Prompt,
                ReadAccessToken(context),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ToOpenAiVideoError(result);
        }

        return Results.Json(
            OpenAiWireMapper.ToAzureVideoGenerationJobResponse(
                result with
                {
                    Size = $"{read.Value.Width}x{read.Value.Height}",
                    Seconds = $"{read.Value.NSeconds}",
                }
            )
        );
    }

    private static async Task<IResult> ListAzureOpenAiVideoJobsAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "listed Azure video fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error
            ?? Results.Json(OpenAiWireMapper.ToAzureVideoGenerationJobListResponse(read.Value!));
    }

    private static async Task<IResult> GetAzureOpenAiVideoJobAsync(
        string jobId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return InvalidRequest("Missing job-id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved Azure video job fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error
            ?? Results.Json(
                OpenAiWireMapper.ToAzureVideoGenerationJobResponse(
                    read.Value! with { VideoId = jobId }
                )
            );
    }

    private static IResult DeleteAzureOpenAiVideoJobAsync(
        string jobId,
        HttpContext context,
        ILlmTckRuntime runtime
    )
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return InvalidRequest("Missing job-id path parameter.");
        }

        var unauthorized = AuthorizeControlRequest(context, runtime);
        return unauthorized ?? Results.NoContent();
    }

    private static async Task<IResult> GetAzureOpenAiVideoGenerationAsync(
        string generationId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(generationId))
        {
            return InvalidRequest("Missing generation-id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved Azure video generation fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error
            ?? Results.Json(
                OpenAiWireMapper.ToAzureVideoGenerationResponse(
                    read.Value! with { GenerationId = generationId }
                )
            );
    }

    private static async Task<IResult> GetAzureOpenAiVideoThumbnailAsync(
        string generationId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(generationId))
        {
            return InvalidRequest("Missing generation-id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved Azure video thumbnail fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error ?? Results.Bytes(_defaultJpegBytes, "image/jpg");
    }

    private static async Task<IResult> GetAzureOpenAiVideoContentAsync(
        string generationId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(generationId))
        {
            return InvalidRequest("Missing generation-id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved Azure video content fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        return read.Error ?? Results.Bytes(read.Value!.Bytes, read.Value.MediaType);
    }

    private static async Task<IResult> HeadAzureOpenAiVideoContentAsync(
        string generationId,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(generationId))
        {
            return InvalidRequest("Missing generation-id path parameter.");
        }

        var read = await GenerateDefaultVideoFixtureAsync(
                context,
                runtime,
                "retrieved Azure video content fixture",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = read.Value!.MediaType;
        context.Response.ContentLength = read.Value.Bytes.Length;
        return Results.Empty;
    }

    private static async Task<VideoFixtureReadResult> GenerateDefaultVideoFixtureAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string prompt,
        CancellationToken cancellationToken
    )
    {
        var modelId = runtime
            .GetModels()
            .FirstOrDefault(model => model.Kind == LlmTckModelKind.Video)
            ?.Id ?? LlmTckKnownModelIds.Sora2;
        var result = await runtime
            .GenerateVideoAsync(modelId, prompt, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? new(result, null)
            : new(null, ToOpenAiVideoError(result));
    }

    private static async Task<IResult> GenerateAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowedResponseFormats: _openAiSpeechResponseFormats,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateGroqAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateAudioCoreAsync(
                context,
                runtime,
                modelOverride: null,
                allowedResponseFormats: _groqSpeechResponseFormats,
                cancellationToken
            )
            .ConfigureAwait(false);
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

    private static async Task WriteStreamingChatAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        var responseId = OpenAiWireMapper.CreateResponseId();
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var chunk in result.StreamChunks)
        {
            var payload = JsonSerializer.Serialize(
                OpenAiWireMapper.ToChatChunk(result, chunk, responseId, created),
                _jsonOptions
            );
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken)
                .ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var finalPayload = JsonSerializer.Serialize(
            OpenAiWireMapper.ToChatChunk(result, string.Empty, responseId, created, "stop"),
            _jsonOptions
        );
        await context.Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteStreamingResponseAsync(
        HttpContext context,
        LlmTckChatResult result,
        string responseId,
        long created,
        OpenAiCacheUsageShape cacheUsageShape,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToResponseCreatedEvent(result, responseId, created),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToResponseOutputItemAddedEvent(responseId),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToResponseContentPartAddedEvent(responseId),
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (var chunk in result.StreamChunks)
        {
            await WriteResponseSseDataAsync(
                    context,
                    OpenAiWireMapper.ToResponseContentPartDeltaEvent(responseId, chunk),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToResponseOutputItemDoneEvent(responseId, result),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToResponseDoneEvent(
                    result,
                    responseId,
                    created,
                    cacheUsageShape
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteResponseSseDataAsync(
        HttpContext context,
        object data,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(data, _jsonOptions);
        await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteStreamingTranscriptionAsync(
        HttpContext context,
        LlmTckTranscriptionResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToTranscriptTextDeltaEvent(result.Text),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToTranscriptTextDoneEvent(result),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteImageStreamingAsync(
        HttpContext context,
        string dataUri,
        string eventPrefix,
        string? background,
        string? outputFormat,
        string? quality,
        string? size,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToImageStreamingEvent(
                    $"{eventPrefix}.partial_image",
                    dataUri,
                    completed: false,
                    background,
                    outputFormat,
                    quality,
                    size
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteResponseSseDataAsync(
                context,
                OpenAiWireMapper.ToImageStreamingEvent(
                    $"{eventPrefix}.completed",
                    dataUri,
                    completed: true,
                    background,
                    outputFormat,
                    quality,
                    size
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteOllamaStreamingChatAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/x-ndjson";

        foreach (var chunk in result.StreamChunks)
        {
            var payload = JsonSerializer.Serialize(
                OllamaWireMapper.ToChatChunk(result, chunk, done: false),
                _jsonOptions
            );
            await context.Response.WriteAsync($"{payload}\n", cancellationToken)
                .ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var finalPayload = JsonSerializer.Serialize(
            OllamaWireMapper.ToChatChunk(result, string.Empty, done: true),
            _jsonOptions
        );
        await context.Response.WriteAsync($"{finalPayload}\n", cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteAnthropicStreamingMessageAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        await WriteAnthropicSseEventAsync(
                context,
                "message_start",
                AnthropicWireMapper.ToMessageStartEvent(result),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteAnthropicSseEventAsync(
                context,
                "content_block_start",
                AnthropicWireMapper.ToContentBlockStartEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (var chunk in result.StreamChunks)
        {
            await WriteAnthropicSseEventAsync(
                    context,
                    "content_block_delta",
                    AnthropicWireMapper.ToContentBlockDeltaEvent(chunk),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await WriteAnthropicSseEventAsync(
                context,
                "content_block_stop",
                AnthropicWireMapper.ToContentBlockStopEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteAnthropicSseEventAsync(
                context,
                "message_delta",
                AnthropicWireMapper.ToMessageDeltaEvent(result),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteAnthropicSseEventAsync(
                context,
                "message_stop",
                AnthropicWireMapper.ToMessageStopEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteAnthropicSseEventAsync(
        HttpContext context,
        string eventName,
        object data,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(data, _jsonOptions);
        await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteCohereStreamingChatAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        await WriteCohereSseEventAsync(
                context,
                "message-start",
                CohereWireMapper.ToMessageStartEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteCohereSseEventAsync(
                context,
                "content-start",
                CohereWireMapper.ToContentStartEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (var chunk in result.StreamChunks)
        {
            await WriteCohereSseEventAsync(
                    context,
                    "content-delta",
                    CohereWireMapper.ToContentDeltaEvent(chunk),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await WriteCohereSseEventAsync(
                context,
                "content-end",
                CohereWireMapper.ToContentEndEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteCohereSseEventAsync(
                context,
                "message-end",
                CohereWireMapper.ToMessageEndEvent(result),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteCohereSseEventAsync(
        HttpContext context,
        string eventName,
        object data,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(data, _jsonOptions);
        await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteGeminiStreamingContentAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";

        foreach (var chunk in result.StreamChunks)
        {
            var payload = JsonSerializer.Serialize(
                GeminiWireMapper.ToGenerateContentResponse(result, chunk, finishReason: null),
                _jsonOptions
            );
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken)
                .ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var finalPayload = JsonSerializer.Serialize(
            GeminiWireMapper.ToGenerateContentResponse(result, string.Empty),
            _jsonOptions
        );
        await context.Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteBedrockConverseStreamAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/vnd.amazon.eventstream";

        await WriteBedrockEventAsync(
                context,
                BedrockWireMapper.ToConverseMessageStartEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteBedrockEventAsync(
                context,
                BedrockWireMapper.ToConverseContentBlockStartEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (var chunk in ReadStreamChunks(result))
        {
            await WriteBedrockEventAsync(
                    context,
                    BedrockWireMapper.ToConverseContentBlockDeltaEvent(chunk),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await WriteBedrockEventAsync(
                context,
                BedrockWireMapper.ToConverseContentBlockStopEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteBedrockEventAsync(
                context,
                BedrockWireMapper.ToConverseMessageStopEvent(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await WriteBedrockEventAsync(
                context,
                BedrockWireMapper.ToConverseMetadataEvent(result),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task WriteBedrockInvokeModelStreamAsync(
        HttpContext context,
        LlmTckChatResult result,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/vnd.amazon.eventstream";
        context.Response.Headers["x-amzn-bedrock-content-type"] = "application/json";

        foreach (var chunk in ReadStreamChunks(result))
        {
            await WriteBedrockEventAsync(
                    context,
                    BedrockWireMapper.ToInvokeStreamChunk(chunk),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteBedrockEventAsync(
        HttpContext context,
        object data,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(data, _jsonOptions);
        await context.Response.WriteAsync($"{payload}\n", cancellationToken)
            .ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ReadStreamChunks(LlmTckChatResult result)
    {
        return result.StreamChunks.Count == 0 ? [result.Content] : result.StreamChunks;
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string Prefix = "Bearer ";
        return header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? header[Prefix.Length..]
            : null;
    }

    private static string? ReadAccessToken(HttpContext context)
    {
        var bearerToken = ReadBearerToken(context);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            return bearerToken;
        }

        var apiKey = context.Request.Headers["api-key"].ToString();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        var anthropicApiKey = context.Request.Headers["x-api-key"].ToString();
        if (!string.IsNullOrWhiteSpace(anthropicApiKey))
        {
            return anthropicApiKey;
        }

        var googleApiKey = context.Request.Headers["x-goog-api-key"].ToString();
        if (!string.IsNullOrWhiteSpace(googleApiKey))
        {
            return googleApiKey;
        }

        var queryKey = context.Request.Query["key"].ToString();
        return string.IsNullOrWhiteSpace(queryKey) ? null : queryKey;
    }

    private static List<string> ReadEmbeddingInputs(JsonElement input)
    {
        return input.ValueKind == JsonValueKind.Array
            ? input
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .ToList()
            : [input.ValueKind == JsonValueKind.String ? input.GetString() ?? string.Empty : input.ToString()];
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
        CancellationToken cancellationToken
    )
    {
        return await ReadJsonAsync<T>(context, InvalidRequest, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<JsonReadResult<T>> ReadJsonAsync<T>(
        HttpContext context,
        Func<string, IResult> invalidRequest,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var value = await context
                .Request
                .ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return new JsonReadResult<T>(value, null);
        }
        catch (JsonException)
        {
            return new JsonReadResult<T>(default, invalidRequest("Malformed JSON request body."));
        }
        catch (BadHttpRequestException)
        {
            return new JsonReadResult<T>(default, invalidRequest("Malformed JSON request body."));
        }
    }

    private static IResult? ValidateAnthropicVersion(HttpContext context)
    {
        var version = context.Request.Headers[AnthropicWireMapper.VersionHeaderName].ToString();
        return string.Equals(version, AnthropicWireMapper.SupportedVersion, StringComparison.Ordinal)
            ? null
            : AnthropicInvalidRequest(
                $"The {AnthropicWireMapper.VersionHeaderName} header must be {AnthropicWireMapper.SupportedVersion}."
            );
    }

    private static IResult? ValidateAnthropicMessageRequest(AnthropicMessagesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return AnthropicInvalidRequest("Missing message model.");
        }

        if (request.MaxTokens is null or < 0)
        {
            return AnthropicInvalidRequest("Missing or invalid max_tokens.");
        }

        if (request.Messages.Count == 0)
        {
            return AnthropicInvalidRequest("At least one message is required.");
        }

        return request.Messages.Any(message =>
                message is null
                || message.Role is not ("user" or "assistant")
                || message.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            )
            ? AnthropicInvalidRequest("Every message requires a user or assistant role and content.")
            : null;
    }

    private static IResult? ValidateOllamaChatRequest(OllamaChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return OllamaInvalidRequest("Missing chat model.");
        }

        if (request.Messages.Count == 0)
        {
            return OllamaInvalidRequest("At least one chat message is required.");
        }

        return request.Messages.Any(message =>
                message is null
                || string.IsNullOrWhiteSpace(message.Role)
                || message.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            )
            ? OllamaInvalidRequest("Every chat message requires a role and content.")
            : null;
    }

    private static IResult? ValidateOllamaEmbeddingRequest(OllamaEmbedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return OllamaInvalidRequest("Missing embedding model.");
        }

        if (request.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return OllamaInvalidRequest("Missing embedding input.");
        }

        return OllamaWireMapper.ReadEmbeddingInputs(request).Count == 0
            ? OllamaInvalidRequest("Missing embedding input.")
            : null;
    }

    private static IResult? ValidateCohereChatRequest(CohereChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return CohereInvalidRequest("Missing chat model.");
        }

        if (request.Messages.Count == 0)
        {
            return CohereInvalidRequest("At least one chat message is required.");
        }

        return request.Messages.Any(message =>
                message is null
                || string.IsNullOrWhiteSpace(message.Role)
                || message.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            )
            ? CohereInvalidRequest("Every chat message requires a role and content.")
            : null;
    }

    private static IResult? ValidateCohereEmbeddingRequest(CohereEmbedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return CohereInvalidRequest("Missing embedding model.");
        }

        if (string.IsNullOrWhiteSpace(request.InputType))
        {
            return CohereInvalidRequest("Missing embedding input_type.");
        }

        return CohereWireMapper.ReadEmbeddingInputs(request).Count == 0
            ? CohereInvalidRequest("Missing embedding texts or inputs.")
            : null;
    }

    private static IResult? ValidateGeminiGenerateContentRequest(
        string model,
        GeminiGenerateContentRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return GeminiInvalidRequest("Missing model path parameter.");
        }

        if (request.Contents.Count == 0)
        {
            return GeminiInvalidRequest("At least one content item is required.");
        }

        return request.Contents.Any(content => content is null || content.Parts.Count == 0)
            ? GeminiInvalidRequest("Every content item requires at least one part.")
            : null;
    }

    private static IResult? ValidateGeminiEmbeddingRequest(
        string model,
        GeminiEmbedContentRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return GeminiInvalidRequest("Missing model path parameter.");
        }

        if (request.Content.Parts.Count == 0)
        {
            return GeminiInvalidRequest("Embedding content requires at least one part.");
        }

        return string.IsNullOrWhiteSpace(GeminiWireMapper.ReadText(request.Content))
            ? GeminiInvalidRequest("Embedding content requires text.")
            : null;
    }

    private static IResult? ValidateGeminiPredictLongRunningRequest(
        string model,
        GeminiPredictLongRunningRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return GeminiInvalidRequest("Missing model path parameter.");
        }

        if (request.Instances.Count == 0)
        {
            return GeminiInvalidRequest("At least one prediction instance is required.");
        }

        return string.IsNullOrWhiteSpace(GeminiWireMapper.ReadPredictPrompt(request))
            ? GeminiInvalidRequest("Video prediction instances require a prompt.")
            : null;
    }

    private static IResult? ValidateBedrockConverseRequest(
        string modelId,
        BedrockConverseRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return BedrockInvalidRequest("Missing modelId path parameter.");
        }

        if (request.Messages.Count == 0)
        {
            return BedrockInvalidRequest("At least one message is required.");
        }

        return request.Messages.Any(message =>
                message is null
                || message.Role is not ("user" or "assistant")
                || message.Content.Count == 0
            )
            ? BedrockInvalidRequest("Every message requires a user or assistant role and content.")
            : null;
    }

    private static IResult? ValidateBedrockInvokeRequest(string modelId, JsonElement request)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return BedrockInvalidRequest("Missing modelId path parameter.");
        }

        return string.IsNullOrWhiteSpace(BedrockWireMapper.ReadInvokeInput(request))
            ? BedrockInvalidRequest("Missing invoke input.")
            : null;
    }

    private static IResult? ValidateChatRequest(OpenAiChatCompletionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing chat completion model.");
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            return InvalidRequest("At least one chat message is required.");
        }

        return request.Messages.Any(message =>
                message is null
                || string.IsNullOrWhiteSpace(message.Role)
                || message.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            )
            ? InvalidRequest("Every chat message requires a role and content.")
            : null;
    }

    private static IResult? ValidateResponseRequest(OpenAiResponseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing response model.");
        }

        return request.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? InvalidRequest("Missing response input.")
            : null;
    }

    private static IResult? ValidateEmbeddingRequest(OpenAiEmbeddingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing embedding model.");
        }

        return request.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? InvalidRequest("Missing embedding input.")
            : null;
    }

    private static IResult? ValidateImageRequest(OpenAiImageGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing image model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return InvalidRequest("Missing image prompt.");
        }

        if (!IsNullOrAllowed(request.ResponseFormat, _openAiImageResponseFormats))
        {
            return InvalidRequest("Unsupported image response_format.");
        }

        if (!IsNullOrAllowed(request.OutputFormat, _openAiImageOutputFormats))
        {
            return InvalidRequest("Unsupported image output_format.");
        }

        if (!IsNullOrAllowed(request.Quality, _openAiImageQualities))
        {
            return InvalidRequest("Unsupported image quality.");
        }

        if (!IsNullOrAllowed(request.Background, _openAiImageBackgrounds))
        {
            return InvalidRequest("Unsupported image background.");
        }

        if (!IsNullOrAllowed(request.Size, _openAiImageSizes))
        {
            return InvalidRequest("Unsupported image size.");
        }

        return request.PartialImages is null or >= 0 and <= 3
            ? null
            : InvalidRequest("Image partial_images must be between 0 and 3.");
    }

    private static IResult? ValidateImageEditRequest(OpenAiImageEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing image edit model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return InvalidRequest("Missing image edit prompt.");
        }

        var hasJsonImage = request.Image.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;
        if (request.Images.Count == 0 && !hasJsonImage)
        {
            return InvalidRequest("Image edits require at least one image.");
        }

        if (!IsNullOrAllowed(request.OutputFormat, _openAiImageOutputFormats))
        {
            return InvalidRequest("Unsupported image edit output_format.");
        }

        if (!IsNullOrAllowed(request.Quality, _openAiImageQualities))
        {
            return InvalidRequest("Unsupported image edit quality.");
        }

        if (!IsNullOrAllowed(request.Background, _openAiImageBackgrounds))
        {
            return InvalidRequest("Unsupported image edit background.");
        }

        if (!IsNullOrAllowed(request.Size, _openAiImageSizes))
        {
            return InvalidRequest("Unsupported image edit size.");
        }

        return request.PartialImages is null or >= 0 and <= 3
            ? null
            : InvalidRequest("Image edit partial_images must be between 0 and 3.");
    }

    private static IResult? ValidateImageVariationRequest(OpenAiImageVariationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing image variation model.");
        }

        if (request.Count is < 1 or > 10)
        {
            return InvalidRequest("Image variation n must be between 1 and 10.");
        }

        if (!IsNullOrAllowed(request.ResponseFormat, _openAiImageResponseFormats))
        {
            return InvalidRequest("Unsupported image variation response_format.");
        }

        return IsNullOrAllowed(request.Size, _openAiImageVariationSizes)
            ? null
            : InvalidRequest("Unsupported image variation size.");
    }

    private static IResult? ValidateAudioRequest(
        OpenAiAudioSpeechRequest request,
        string[] allowedResponseFormats
    )
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing audio model.");
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return InvalidRequest("Missing audio input.");
        }

        if (string.IsNullOrWhiteSpace(request.Voice))
        {
            return InvalidRequest("Missing audio voice.");
        }

        return string.IsNullOrWhiteSpace(request.ResponseFormat)
            || IsSupportedAudioResponseFormat(request.ResponseFormat, allowedResponseFormats)
            ? null
            : InvalidRequest("Unsupported audio response_format.");
    }

    private static IResult ToOpenAiVideoError(LlmTckVideoResult result)
    {
        return Results.Json(
            OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
            statusCode: result.StatusCode
        );
    }

    private static IResult ToGeminiVideoError(LlmTckVideoResult result)
    {
        return Results.Json(
            GeminiWireMapper.ToError(result.StatusCode, result.ErrorMessage!),
            statusCode: result.StatusCode
        );
    }

    private static string CreateGeminiFileUri(HttpContext context, string fileId, bool media)
    {
        var path = ProviderRoutes.ForProvider(
            ProviderRoutes.Gemini,
            $"/v1beta/files/{Uri.EscapeDataString(fileId)}"
        );
        var query = media ? "?alt=media" : string.Empty;

        return context.Request.Host.HasValue
            ? $"{context.Request.Scheme}://{context.Request.Host}{path}{query}"
            : $"{path}{query}";
    }

    private static IResult? ValidateOpenAiVideoCreateRequest(OpenAiVideoCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing video model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return InvalidRequest("Missing video prompt.");
        }

        if (!IsAllowedOpenAiVideoSeconds(request.Seconds))
        {
            return InvalidRequest("Unsupported video seconds.");
        }

        return IsAllowedOpenAiVideoSize(request.Size)
            ? null
            : InvalidRequest("Unsupported video size.");
    }

    private static IResult? ValidateOpenAiVideoListQuery(HttpContext context)
    {
        var order = context.Request.Query["order"].ToString();
        if (!string.IsNullOrWhiteSpace(order) && order is not ("asc" or "desc"))
        {
            return InvalidRequest("Unsupported video list order.");
        }

        var limit = context.Request.Query["limit"].ToString();
        return string.IsNullOrWhiteSpace(limit)
            || (int.TryParse(limit, out var value) && value is >= 0 and <= 100)
            ? null
            : InvalidRequest("Unsupported video list limit.");
    }

    private static IResult? ValidateAzureVideoGenerationJobRequest(
        AzureVideoGenerationJobRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing video generation model.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return InvalidRequest("Missing video generation prompt.");
        }

        if (request.Width <= 0 || request.Height <= 0)
        {
            return InvalidRequest("Video generation width and height must be positive.");
        }

        if (request.NSeconds is < 1 or > 20)
        {
            return InvalidRequest("Video generation n_seconds must be between 1 and 20.");
        }

        return request.NVariants is < 1 or > 5
            ? InvalidRequest("Video generation n_variants must be between 1 and 5.")
            : null;
    }

    private static bool IsAllowedOpenAiVideoSeconds(string? seconds)
    {
        return string.IsNullOrWhiteSpace(seconds)
            || Array.Exists(
                _openAiVideoSeconds,
                value => string.Equals(value, seconds, StringComparison.Ordinal)
            );
    }

    private static bool IsAllowedOpenAiVideoExtensionSeconds(string? seconds)
    {
        return string.IsNullOrWhiteSpace(seconds)
            || Array.Exists(
                _openAiVideoExtensionSeconds,
                value => string.Equals(value, seconds, StringComparison.Ordinal)
            );
    }

    private static bool IsAllowedOpenAiVideoSize(string? size)
    {
        return string.IsNullOrWhiteSpace(size)
            || Array.Exists(
                _openAiVideoSizes,
                value => string.Equals(value, size, StringComparison.Ordinal)
            );
    }

    private static bool IsNullOrAllowed(string? value, string[] allowedValues)
    {
        return string.IsNullOrWhiteSpace(value)
            || Array.Exists(allowedValues, item => string.Equals(item, value, StringComparison.Ordinal));
    }

    private static int? TryReadInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IResult InvalidRequest(string message)
    {
        return Results.Json(
            OpenAiWireMapper.ToError("invalid_request", message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static IResult AnthropicInvalidRequest(string message)
    {
        return Results.Json(
            AnthropicWireMapper.ToError("invalid_request_error", message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static IResult OllamaInvalidRequest(string message)
    {
        return Results.Json(
            OllamaWireMapper.ToError(message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static IResult CohereInvalidRequest(string message)
    {
        return Results.Json(
            CohereWireMapper.ToError(message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static IResult GeminiInvalidRequest(string message)
    {
        return Results.Json(
            GeminiWireMapper.ToError(StatusCodes.Status400BadRequest, message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static IResult BedrockInvalidRequest(string message)
    {
        return Results.Json(
            BedrockWireMapper.ToError(message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private static string ToAnthropicErrorType(int statusCode, string code)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => "authentication_error",
            StatusCodes.Status403Forbidden => "permission_error",
            StatusCodes.Status404NotFound => "not_found_error",
            StatusCodes.Status413PayloadTooLarge => "request_too_large",
            StatusCodes.Status429TooManyRequests => "rate_limit_error",
            >= StatusCodes.Status500InternalServerError => statusCode == 529
                ? "overloaded_error"
                : "api_error",
            _ when string.Equals(code, "invalid_request", StringComparison.Ordinal) => "invalid_request_error",
            _ => "invalid_request_error",
        };
    }

    private readonly record struct JsonReadResult<T>(T? Value, IResult? Error);

    private readonly record struct FormReadResult(AudioFormRequest Value, IResult? Error);

    private readonly record struct VideoFixtureReadResult(LlmTckVideoResult? Value, IResult? Error);

    private readonly record struct AudioFormRequest(
        string Model,
        string FileName,
        string? Prompt,
        string ResponseFormat,
        bool Stream
    );

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<LlmTckModelKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<LlmTckMatchMode>(JsonNamingPolicy.CamelCase));
        return options;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Hosting;

public static class LlmTckEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

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

        return services;
    }

    public static IEndpointRouteBuilder MapLlmTck(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/__llm-tck/models",
            (HttpContext context, ILlmTckRuntime runtime) =>
                AuthorizeControlRequest(context, runtime) ?? Results.Json(runtime.GetModels())
        );
        endpoints.MapGet(
            "/__llm-tck/assertions",
            (HttpContext context, ILlmTckRuntime runtime) =>
                AuthorizeControlRequest(context, runtime) ?? Results.Json(runtime.GetAssertionSummary())
        );
        endpoints.MapPost(
            "/__llm-tck/reset",
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
        endpoints.MapPost("/__llm-tck/configure", ConfigureAsync);

        endpoints.MapGet(
            "/v1/models",
            (ILlmTckRuntime runtime) => Results.Json(OpenAiWireMapper.ToModelsResponse(runtime.GetModels()))
        );
        endpoints.MapPost("/v1/chat/completions", CompleteChatAsync);
        endpoints.MapPost("/v1/embeddings", CreateEmbeddingAsync);
        endpoints.MapPost("/v1/images/generations", GenerateImageAsync);
        endpoints.MapPost("/v1/audio/speech", GenerateAudioAsync);
        endpoints.MapPost("/chat/completions", CompleteChatAsync);
        endpoints.MapPost("/embeddings", CreateEmbeddingAsync);
        endpoints.MapPost("/models/chat/completions", CompleteChatAsync);
        endpoints.MapPost("/models/embeddings", CreateEmbeddingAsync);
        endpoints.MapPost(
            "/openai/deployments/{deployment}/chat/completions",
            CompleteAzureOpenAiChatAsync
        );
        endpoints.MapPost(
            "/openai/deployments/{deployment}/embeddings",
            CreateAzureOpenAiEmbeddingAsync
        );
        endpoints.MapPost(
            "/openai/deployments/{deployment}/images/generations",
            GenerateAzureOpenAiImageAsync
        );
        endpoints.MapPost(
            "/openai/deployments/{deployment}/audio/speech",
            GenerateAzureOpenAiAudioAsync
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
        return await CompleteChatCoreAsync(context, runtime, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteAzureOpenAiChatAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await CompleteChatCoreAsync(context, runtime, deployment, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CompleteChatCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
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
        var validationError = ValidateChatRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var result = await runtime
            .CompleteChatAsync(
                OpenAiWireMapper.ToRuntimeRequest(request),
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

        return Results.Json(OpenAiWireMapper.ToChatResponse(result));
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

    private static async Task<IResult> GenerateImageAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateImageCoreAsync(context, runtime, null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateAzureOpenAiImageAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateImageCoreAsync(context, runtime, deployment, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateImageCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
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

        var result = await runtime
            .GenerateImageAsync(request.Model, request.Prompt, ReadAccessToken(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Json(OpenAiWireMapper.ToImageResponse(result.DataUri))
            : Results.Json(
                OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!),
                statusCode: result.StatusCode
            );
    }

    private static async Task<IResult> GenerateAudioAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateAudioCoreAsync(context, runtime, null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateAzureOpenAiAudioAsync(
        string deployment,
        HttpContext context,
        ILlmTckRuntime runtime,
        CancellationToken cancellationToken
    )
    {
        return await GenerateAudioCoreAsync(context, runtime, deployment, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> GenerateAudioCoreAsync(
        HttpContext context,
        ILlmTckRuntime runtime,
        string? modelOverride,
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
        var validationError = ValidateAudioRequest(request);
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
        return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
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

    private static async Task<JsonReadResult<T>> ReadJsonAsync<T>(
        HttpContext context,
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
            return new JsonReadResult<T>(default, InvalidRequest("Malformed JSON request body."));
        }
        catch (BadHttpRequestException)
        {
            return new JsonReadResult<T>(default, InvalidRequest("Malformed JSON request body."));
        }
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

        return string.IsNullOrWhiteSpace(request.Prompt)
            ? InvalidRequest("Missing image prompt.")
            : null;
    }

    private static IResult? ValidateAudioRequest(OpenAiAudioSpeechRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return InvalidRequest("Missing audio model.");
        }

        return string.IsNullOrWhiteSpace(request.Input)
            ? InvalidRequest("Missing audio input.")
            : null;
    }

    private static IResult InvalidRequest(string message)
    {
        return Results.Json(
            OpenAiWireMapper.ToError("invalid_request", message),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    private readonly record struct JsonReadResult<T>(T? Value, IResult? Error);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<LlmTckModelKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<LlmTckMatchMode>(JsonNamingPolicy.CamelCase));
        return options;
    }
}

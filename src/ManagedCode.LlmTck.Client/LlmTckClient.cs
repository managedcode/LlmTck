using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Runtime;
using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckClient(HttpClient httpClient, string? bearerToken = null)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static LlmTckClient Create(HttpClient httpClient, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return new LlmTckClient(httpClient, bearerToken);
    }

    public LlmTckClient WithBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new LlmTckClient(httpClient, token);
    }

    public Task ConfigureAsync(
        Action<LlmTckClientConfigurationBuilder> configure,
        CancellationToken cancellationToken = default
    )
    {
        return ConfigureAsync(configure, resetFirst: false, cancellationToken);
    }

    public async Task ConfigureAsync(
        Action<LlmTckClientConfigurationBuilder> configure,
        bool resetFirst,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (resetFirst)
        {
            await ResetAsync(cancellationToken).ConfigureAwait(false);
        }

        var builder = new LlmTckClientConfigurationBuilder();
        configure(builder);
        await ConfigureAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
    }

    public async Task ConfigureAsync(
        LlmTckConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        using var request = CreateJsonRequest(HttpMethod.Post, "/__llm-tck/configure", configuration);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/__llm-tck/reset");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LlmTckAssertionSummary> GetAssertionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var request = CreateRequest(HttpMethod.Get, "/__llm-tck/assertions");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response
            .Content
            .ReadFromJsonAsync<LlmTckAssertionSummary>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? new LlmTckAssertionSummary();
    }

    public IChatClient CreateChatClient(string defaultModelId = "llm-tck-chat")
    {
        return new LlmTckChatClient(httpClient, defaultModelId, bearerToken);
    }

    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        string defaultModelId = "llm-tck-embedding"
    )
    {
        return new LlmTckEmbeddingGenerator(httpClient, defaultModelId, bearerToken);
    }

    public IImageGenerator CreateImageGenerator(string defaultModelId = "llm-tck-image")
    {
        return new LlmTckImageGenerator(httpClient, defaultModelId, bearerToken);
    }

    public async Task<LlmTckAudioContent> GenerateAudioAsync(
        string modelId,
        string input,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(input);

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/v1/audio/speech",
            new { model = modelId, input }
        );
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return new LlmTckAudioContent(
            await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
        );
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        ApplyBearerToken(request);
        return request;
    }

    private HttpRequestMessage CreateJsonRequest<TPayload>(
        HttpMethod method,
        string requestUri,
        TPayload payload
    )
    {
        var request = CreateRequest(method, requestUri);
        request.Content = JsonContent.Create(payload, options: _jsonOptions);
        return request;
    }

    private void ApplyBearerToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}

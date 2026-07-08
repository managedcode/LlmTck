using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.OpenAI;
using Microsoft.Extensions.AI;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckEmbeddingGenerator(
    HttpClient httpClient,
    string defaultModelId = "llm-tck-embedding",
    string? bearerToken = null
) : IEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        var request = new
        {
            model = options?.ModelId ?? defaultModelId,
            input = values.ToArray(),
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/embeddings")
        )
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        ApplyBearerToken(httpRequest);

        using var response = await httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response
            .Content
            .ReadFromJsonAsync<OpenAiEmbeddingResponse>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var embeddings =
            payload
                ?.Data
                .OrderBy(item => item.Index)
                .Select(item => new Embedding<float>(item.Vector) { ModelId = payload.Model })
                .ToList()
            ?? [];

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private void ApplyBearerToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.OpenAI;
using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckEmbeddingGenerator(
    HttpClient httpClient,
    string defaultModelId = "llm-tck-embedding"
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

        using var response = await httpClient
            .PostAsJsonAsync("/v1/embeddings", request, _jsonOptions, cancellationToken)
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
                .Select(item => new Embedding<float>(item.Embedding.ToArray()) { ModelId = payload.Model })
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
}

using System.Net.Http.Json;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Runtime;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckClient(HttpClient httpClient)
{
    public async Task ConfigureAsync(
        LlmTckConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        using var response = await httpClient
            .PostAsJsonAsync("/__llm-tck/configure", configuration, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient
            .PostAsync("/__llm-tck/reset", content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LlmTckAssertionSummary> GetAssertionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await httpClient
            .GetFromJsonAsync<LlmTckAssertionSummary>("/__llm-tck/assertions", cancellationToken)
            .ConfigureAwait(false)
        ?? new LlmTckAssertionSummary();
    }
}

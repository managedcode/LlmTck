using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ManagedCode.LlmTck.OpenAI;
using Microsoft.Extensions.AI;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Client;

public sealed class LlmTckChatClient(
    HttpClient httpClient,
    string defaultModelId = "llm-tck-chat",
    string? bearerToken = null
) : IChatClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ChatClientMetadata Metadata { get; } =
        new(nameof(LlmTckChatClient), httpClient.BaseAddress, defaultModelId);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(messages);

        var request = CreateRequest(messages, options, stream: false);
        using var httpRequest = CreateJsonRequest(request);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var completion = await response
            .Content
            .ReadFromJsonAsync<OpenAiChatCompletionResponse>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var text = completion?.Choices.FirstOrDefault()?.Message.TextContent ?? string.Empty;
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = completion?.Model,
            CreatedAt = DateTimeOffset.UtcNow,
            ResponseId = completion?.Id,
            FinishReason = ChatFinishReason.Stop,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(messages);

        var request = CreateRequest(messages, options, stream: true);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/chat/completions")
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json"
            ),
        };
        ApplyBearerToken(httpRequest);

        using var response = await httpClient
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response
            .Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data: ".Length..];
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                yield break;
            }

            var chunk = JsonSerializer.Deserialize<OpenAiChatCompletionChunk>(payload, _jsonOptions);
            var choice = chunk?.Choices.FirstOrDefault();
            var text = choice?.Delta.TextContent;
            if (!string.IsNullOrEmpty(text))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, text)
                {
                    ModelId = chunk?.Model,
                    ResponseId = chunk?.Id,
                    FinishReason = choice?.FinishReason == "stop" ? ChatFinishReason.Stop : null,
                };
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private OpenAiChatCompletionRequest CreateRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        bool stream
    )
    {
        return new()
        {
            Model = options?.ModelId ?? defaultModelId,
            Stream = stream,
            Messages = messages
                .Select(message => new OpenAiChatMessage
                {
                    Role = message.Role.Value,
                    Content = OpenAiChatMessage.FromText(message.Role.Value, message.Text).Content,
                })
                .ToList(),
        };
    }

    private HttpRequestMessage CreateJsonRequest(OpenAiChatCompletionRequest request)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, "/v1/chat/completions")
        )
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        ApplyBearerToken(httpRequest);
        return httpRequest;
    }

    private void ApplyBearerToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}

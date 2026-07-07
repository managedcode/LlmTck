using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Tests.TestSupport;

internal sealed class TestChatClient : IChatClient
{
    public Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>>?
        GetResponseAsyncCallback
    {
        get;
        init;
    }

    public Func<
        IEnumerable<ChatMessage>,
        ChatOptions?,
        CancellationToken,
        IAsyncEnumerable<ChatResponseUpdate>
    >? GetStreamingResponseAsyncCallback
    {
        get;
        init;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetResponseAsyncCallback?.Invoke(messages, options, cancellationToken)
        ?? throw new InvalidOperationException("GetResponseAsyncCallback was not configured.");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        return GetStreamingResponseAsyncCallback?.Invoke(messages, options, cancellationToken)
        ?? throw new InvalidOperationException(
            "GetStreamingResponseAsyncCallback was not configured."
        );
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}

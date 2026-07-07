namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckChatRequest
{
    public string ModelId { get; init; } = "llm-tck-chat";

    public List<LlmTckMessage> Messages { get; init; } = [];

    public bool Stream { get; init; }
}

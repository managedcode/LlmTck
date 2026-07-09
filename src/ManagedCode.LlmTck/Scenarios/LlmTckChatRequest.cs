using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckChatRequest
{
    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public List<LlmTckMessage> Messages { get; init; } = [];

    public bool Stream { get; init; }
}

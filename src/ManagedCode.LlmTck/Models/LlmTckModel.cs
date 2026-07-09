namespace ManagedCode.LlmTck.Models;

public sealed record LlmTckModel
{
    public string Id { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public LlmTckModelKind Kind { get; init; } = LlmTckModelKind.Chat;

    public string OwnedBy { get; init; } = "llm-tck";

    public int ReasoningTokens { get; init; }
}

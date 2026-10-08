namespace ManagedCode.LlmTck.Models;

public sealed record LlmTckModel
{
    public string Id { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public LlmTckModelKind Kind { get; init; } = LlmTckModelKind.Chat;

    public string OwnedBy { get; init; } = "llm-tck";

    public ManagedCode.LlmTck.Decisions.LlmTckDecisionProvider DecisionProvider { get; init; }

    public int ReasoningTokens { get; init; }
}

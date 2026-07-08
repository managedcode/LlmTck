namespace ManagedCode.LlmTck.Runtime;

public sealed record LlmTckTokenUsage
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public int TotalTokens { get; init; }
}

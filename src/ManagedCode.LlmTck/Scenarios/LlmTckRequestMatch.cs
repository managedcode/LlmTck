namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckRequestMatch
{
    public LlmTckMatchMode Mode { get; init; } = LlmTckMatchMode.Contains;

    public List<LlmTckMessage> Messages { get; init; } = [];
}

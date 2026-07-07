namespace ManagedCode.LlmTck.Models;

public sealed record LlmTckModel
{
    public string Id { get; init; } = "llm-tck-chat";

    public LlmTckModelKind Kind { get; init; } = LlmTckModelKind.Chat;

    public string OwnedBy { get; init; } = "llm-tck";
}

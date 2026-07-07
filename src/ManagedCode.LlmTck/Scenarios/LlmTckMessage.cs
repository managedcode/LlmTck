namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckMessage
{
    public string Role { get; init; } = "user";

    public string Content { get; init; } = string.Empty;
}

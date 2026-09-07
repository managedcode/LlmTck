namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ParametersJson { get; init; } = "{\"type\":\"object\"}";
}

public sealed record LlmTckToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ArgumentsJson { get; init; } = "{}";
}

public enum LlmTckToolChoice { Auto, None, Required }

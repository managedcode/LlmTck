namespace ManagedCode.LlmTck.Providers;

public sealed record LlmTckProviderApiContract
{
    public required string DocumentationUrl { get; init; }

    public required string DocumentationRetrievedOn { get; init; }

    public string? DocumentationVersion { get; init; }

    public IReadOnlyList<LlmTckProviderOperation> Operations { get; init; } = [];
}

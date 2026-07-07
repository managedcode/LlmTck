namespace ManagedCode.LlmTck.Providers;

public sealed record LlmTckProviderProfile
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required LlmTckProtocolFamily Protocol { get; init; }

    public string DefaultEndpointPath { get; init; } = "/";

    public IReadOnlyList<LlmTckProviderCapability> Capabilities { get; init; } = [];

    public IReadOnlyList<string> CompatibilityTags { get; init; } = [];
}

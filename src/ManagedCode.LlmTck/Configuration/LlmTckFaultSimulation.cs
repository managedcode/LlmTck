namespace ManagedCode.LlmTck.Configuration;

public sealed record LlmTckFaultSimulation
{
    public int? MaxRequestsBeforeRateLimit { get; init; }

    public List<string> ContentFilterTerms { get; init; } = [];
}

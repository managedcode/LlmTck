using ManagedCode.LlmTck.Models;

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckScenario
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string ModelId { get; init; } = LlmTckKnownModelIds.Gpt41Mini;

    public string? RequiredBearerToken { get; init; }

    public LlmTckRequestMatch Match { get; init; } = new();

    public List<LlmTckScenarioResponse> Responses { get; init; } = [];
}

namespace ManagedCode.LlmTck.Scenarios;

public sealed record LlmTckScenarioDataset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public List<LlmTckScenario> ChatScenarios { get; init; } = [];
}

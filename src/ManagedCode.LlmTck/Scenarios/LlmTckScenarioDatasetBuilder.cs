using ManagedCode.LlmTck.Configuration;

namespace ManagedCode.LlmTck.Scenarios;

public sealed class LlmTckScenarioDatasetBuilder(string id)
{
    private readonly LlmTckScenarioDataset _dataset = new() { Id = id };

    public LlmTckScenarioDatasetBuilder AddChatScenario(
        string scenarioId,
        Action<LlmTckScenarioBuilder> configure
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new LlmTckScenarioBuilder(scenarioId);
        configure(builder);
        var scenario = builder.Build();

        _dataset.ChatScenarios.RemoveAll(existing =>
            string.Equals(existing.Id, scenarioId, StringComparison.OrdinalIgnoreCase)
        );
        _dataset.ChatScenarios.Add(scenario);

        return this;
    }

    public LlmTckScenarioDataset Build()
    {
        return _dataset with
        {
            ChatScenarios =
            [
                .. _dataset.ChatScenarios.Select(LlmTckConfigurationBuilder.SnapshotScenario),
            ],
        };
    }
}

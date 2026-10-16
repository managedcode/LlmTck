using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Runtime;

public sealed class LlmTckRuntimeToolJournalTests
{
    [Test]
    public async Task OfferedToolsRemainStructuredAndIndependentFromTheCallerAsync()
    {
        var tools = new List<LlmTckToolDefinition>
        {
            new() { Name = "search", Description = "Find a document", ParametersJson = "{\"type\":\"object\"}" },
            new() { Name = "read", Description = null }
        };
        var runtime = new LlmTckRuntime();
        await runtime.CompleteChatAsync(new LlmTckChatRequest { ModelId = "missing", Tools = tools });
        tools.Clear();
        var entry = runtime.GetAssertionSummary().Events.Single();
        await Assert.That(entry.PayloadTruncated).IsFalse();
        await Assert.That(entry.Tools.Select(tool => tool.Name)).IsEquivalentTo(["search", "read"]);
        await Assert.That(entry.Tools[0].Description).IsEqualTo("Find a document");
        await Assert.That(entry.Tools[0].ParametersJson).IsEqualTo("{\"type\":\"object\"}");
        await Assert.That(entry.Tools[1].Description).IsNull();
        await Assert.That(() => ((IList<LlmTckToolDefinition>)entry.Tools).Clear()).Throws<NotSupportedException>();
    }

    [Test]
    public async Task EmptyToolFanoutIsBoundedByTheJournalBudgetAsync()
    {
        const int sourceCount = 33000;
        var runtime = new LlmTckRuntime();
        await runtime.CompleteChatAsync(new LlmTckChatRequest
        {
            ModelId = "missing",
            Tools = Enumerable.Range(0, sourceCount).Select(_ => new LlmTckToolDefinition
            { Name = string.Empty, ParametersJson = string.Empty }).ToList()
        });
        var entry = runtime.GetAssertionSummary().Events.Single();
        await Assert.That(entry.PayloadTruncated).IsTrue();
        await Assert.That(entry.Tools.Count).IsGreaterThan(0).And.IsLessThan(sourceCount);
        await Assert.That(entry.Tools.Count * 256).IsLessThanOrEqualTo(8 * 1024 * 1024);
    }
    [Test]
    public async Task OversizedToolSchemaIsTruncatedAndDoesNotEscapeThePayloadBudgetAsync()
    {
        const int budget = 8 * 1024 * 1024;
        var runtime = new LlmTckRuntime();
        await runtime.CompleteChatAsync(new LlmTckChatRequest
        {
            ModelId = "missing",
            Tools = [new() { Name = "search", ParametersJson = "{\"type\":\"object\",\"description\":\"" + string.Concat(Enumerable.Repeat("word ", budget / 5 + 1)) + "\"}" }]
        });
        var entry = runtime.GetAssertionSummary().Events.Single();
        await Assert.That(entry.PayloadTruncated).IsTrue();
        await Assert.That(entry.Tools.Single().Name).IsEqualTo("search");
        await Assert.That(entry.Tools.Single().ParametersJson.Length).IsGreaterThan(0).And.IsLessThan(budget);
    }
}

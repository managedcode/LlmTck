using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class ReviewRuntimeRegressionTests
{
    private static LlmTckChatRequest Request(string content = "whale")
    {
        return new()
        {
            ModelId = LlmTckKnownModelIds.Gpt41Mini,
            Messages = [new() { Role = "user", Content = content }],
        };
    }

    [Test]
    public async Task PromptCache_EvictsOldestPrefixAtConfiguredCapacityAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().WithPromptCacheCapacity(1)
            .AddChatScenario("queue", s => s.Responds("a").Responds("b").Responds("c").Responds("d")).Build());
        var first = Request("first whale") with { PromptCachePolicy = LlmTckPromptCachePolicy.Ollama };
        var second = Request("second whale") with { PromptCachePolicy = LlmTckPromptCachePolicy.Ollama };
        await runtime.CompleteChatAsync(first);
        await Assert.That((await runtime.CompleteChatAsync(first)).Usage.CachedInputTokens).IsGreaterThan(0);
        await runtime.CompleteChatAsync(second);
        await Assert.That((await runtime.CompleteChatAsync(first)).Usage.CachedInputTokens).IsEqualTo(0);
    }

    [Test]
    public async Task PreCancelledRequest_PreservesFirstResponseAndJournalAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder()
            .AddChatScenario("queue", s => s.Responds("first").Responds("second")).Build());
        await Assert.That(async () => { await runtime.CompleteChatAsync(Request(), cancellationToken: new CancellationToken(true)); })
            .Throws<OperationCanceledException>();
        await Assert.That(runtime.GetAssertionSummary().TotalEvents).IsEqualTo(0);
        await Assert.That((await runtime.CompleteChatAsync(Request())).Content).IsEqualTo("first");
    }

    [Test]
    public async Task ConcurrentCancellation_ReturnsReservationWithoutReplayingCompletedResponseAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder()
            .AddChatScenario("queue", s => s.Responds("first").DelaysBy(100).Responds("second")).Build());
        using var cancellation = new CancellationTokenSource();
        var first = runtime.CompleteChatAsync(Request(), cancellationToken: cancellation.Token);
        // CompleteChatAsync reserves synchronously before its first await; no timing sleep is needed.
        await Assert.That((await runtime.CompleteChatAsync(Request())).Content).IsEqualTo("second");
        await cancellation.CancelAsync();
        await Assert.That(async () => await first).Throws<OperationCanceledException>();
        await Assert.That((await runtime.CompleteChatAsync(Request())).Content).IsEqualTo("first");
        await Assert.That((await runtime.CompleteChatAsync(Request())).StatusCode).IsEqualTo(409);
        await Assert.That(runtime.GetAssertionSummary().Matched).IsEqualTo(2);
    }

    [Test]
    public async Task SameLocalScenarioId_RemainsIndependentAcrossDatasetsAndResetAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder()
            .AddDataset("a", d => d.AddChatScenario("greeting", s => s.WhenUserContains("first").Responds("A")))
            .AddDataset("b", d => d.AddChatScenario("greeting", s => s.WhenUserContains("second").Responds("B")))
            .Build());
        for (var iteration = 0; iteration < 2; iteration++)
        {
            await Assert.That((await runtime.CompleteChatAsync(Request("first"))).Content).IsEqualTo("A");
            await Assert.That((await runtime.CompleteChatAsync(Request("second"))).Content).IsEqualTo("B");
            await runtime.ResetAsync();
        }
    }

    [Test]
    public async Task RejectedAndScriptedErrorRequests_DoNotGenerateReasoningOrOutputUsageAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder()
            .RequireBearerToken("test-key").AddReasoningChatModel(LlmTckKnownModelIds.Gpt41Mini, 17)
            .AddChatScenario("queue", s => s.WhenUserContains("whale").Fails(503, "unavailable", "failed request").Responds("blue whale"))
            .Build());
        var unauthorized = await runtime.CompleteChatAsync(Request());
        var unmatched = await runtime.CompleteChatAsync(Request("other"), "test-key");
        var scripted = await runtime.CompleteChatAsync(Request(), "test-key");
        foreach (var response in new[] { unauthorized, unmatched, scripted })
        {
            await Assert.That(response.IsSuccess).IsFalse();
            await Assert.That(response.Usage.OutputTokens).IsEqualTo(0);
            await Assert.That(response.Usage.ReasoningTokens).IsEqualTo(0);
            await Assert.That(response.Usage.TotalTokens).IsEqualTo(response.Usage.InputTokens);
        }
        await Assert.That(runtime.GetAssertionSummary().OutputTokens).IsEqualTo(0);
        var successful = await runtime.CompleteChatAsync(Request(), "test-key");
        await Assert.That(successful.Usage.ReasoningTokens).IsEqualTo(17);
        await Assert.That(successful.Usage.OutputTokens).IsGreaterThan(17);
    }
}

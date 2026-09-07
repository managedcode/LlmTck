using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class FollowUpRuntimeTests
{
    [Test]
    public async Task VideoStore_DoesNotOverwriteCollidingCallerIdentifiersAsync()
    {
        var runtime = new LlmTckRuntime();
        var first = runtime.StoreVideo("provider", new() { IsSuccess = true, VideoId = "video_2", GenerationId = "generation_3", Bytes = [1] });
        var second = runtime.StoreVideo("provider", new() { IsSuccess = true, VideoId = "video", GenerationId = "generation", Bytes = [2] });
        await Assert.That(second.VideoId).IsEqualTo("video_4");
        await Assert.That(runtime.FindVideo("provider", first.VideoId)!.Bytes.Single()).IsEqualTo((byte)1);
        await Assert.That(runtime.FindVideo("provider", second.GenerationId, generation: true)!.Bytes.Single()).IsEqualTo((byte)2);
    }

    [Test]
    public async Task SummaryMutations_CannotChangeRetainedMessagesOrChunksAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("snapshot", s => s.Responds("answer")).Build());
        var request = new LlmTckChatRequest { RequestId = "snapshot", Stream = true, Messages = [new() { Role = "assistant", ToolCalls = [new() { Id = "original", Name = "weather" }] }] };
        await runtime.CompleteChatAsync(request);
        var snapshot = runtime.GetAssertionSummary();
        var entry = snapshot.Events.Single();
        entry.Messages.Single().ToolCalls.Clear();
        entry.Messages.Single().ToolCalls.Add(new() { Id = "injected", Name = "changed" });
        ((IList<LlmTckMessage>)entry.Messages)[0] = new() { Content = "replaced" };
        ((IList<string>)entry.StreamChunks)[0] = "replaced";
        await Assert.That(runtime.TryGetEvent("snapshot", out var indexed)).IsTrue();
        indexed!.Messages.Single().ToolCalls.Clear();
        ((IList<string>)indexed.StreamChunks)[0] = "index mutation";
        var fresh = runtime.GetAssertionSummary().Events.Single();
        await Assert.That(fresh.Messages.Single().ToolCalls.Single().Id).IsEqualTo("original");
        await Assert.That(fresh.StreamChunks.Single()).IsEqualTo("answer");
        await Assert.That(fresh.Usage).IsEqualTo(entry.Usage);
    }

    [Test]
    public async Task FixtureMismatch_IsCountedAndJournaledWithoutConsumingResponseAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("json", s => s.Responds("not json")).Build());
        var request = new LlmTckChatRequest { RequestId = "request-1", RequireJson = true, Messages = [new() { Content = "hello" }] };
        var rejected = await runtime.CompleteChatAsync(request);
        var summary = runtime.GetAssertionSummary();
        await Assert.That(rejected.ErrorCode).IsEqualTo("llm_tck_fixture_mismatch");
        await Assert.That(summary.TotalEvents).IsEqualTo(1);
        await Assert.That(summary.ErrorsReturned).IsEqualTo(1);
        await Assert.That(summary.TotalTokens).IsEqualTo(rejected.Usage.TotalTokens);
        await Assert.That(summary.Events.Single().RequestId).IsEqualTo("request-1");
        await Assert.That(summary.Events.Single().ScenarioId).IsEqualTo("json");
        await Assert.That(summary.Events.Single().Response).IsEqualTo(rejected.ErrorMessage);
        await Assert.That((await runtime.CompleteChatAsync(request with { RequireJson = false })).Content).IsEqualTo("not json");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExactMatch_RejectsUnexpectedToolMetadataWhileContainsKeepsWildcardsAsync(bool toolResult)
    {
        var expected = new LlmTckMessage { Role = "assistant" };
        var actual = toolResult ? expected with { ToolCallId = "extra" } : expected with { ToolCalls = [new() { Name = "weather" }] };
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("exact", s => s.WithExactMatch(expected).Responds("exact")).Build());
        await Assert.That((await runtime.CompleteChatAsync(new() { Messages = [actual] })).StatusCode).IsEqualTo(404);
        await Assert.That((await runtime.CompleteChatAsync(new() { Messages = [expected] })).Content).IsEqualTo("exact");
        var containsConfiguration = new LlmTckConfigurationBuilder().AddChatScenario("contains", s => s.WithExactMatch(expected).Responds("contains")).Build();
        containsConfiguration.ChatScenarios[0] = containsConfiguration.ChatScenarios[0] with { Match = containsConfiguration.ChatScenarios[0].Match with { Mode = LlmTckMatchMode.Contains } };
        var contains = new LlmTckRuntime(containsConfiguration);
        await Assert.That((await contains.CompleteChatAsync(new() { Messages = [actual] })).Content).IsEqualTo("contains");
    }

    [Test]
    [Arguments(1, 100L)]
    [Arguments(10, 4L)]
    public async Task VideoCapacity_RejectsOverflowAndReclaimsDeletedBytesAsync(int jobs, long bytes)
    {
        var configuration = new LlmTckConfigurationBuilder().WithVideoCapacity(jobs, bytes).Build();
        var runtime = new LlmTckRuntime(configuration);
        var video = new LlmTckVideoResult { IsSuccess = true, VideoId = "video", GenerationId = "generation", ModelId = "sora-2", Bytes = [1, 2, 3, 4] };
        var first = runtime.StoreVideo(LlmTckCompatibilityTags.OpenAI, video);
        var overflow = runtime.StoreVideo(LlmTckCompatibilityTags.AzureOpenAI, video);
        await Assert.That(first.IsSuccess).IsTrue();
        await Assert.That(overflow.StatusCode).IsEqualTo(409);
        await Assert.That(overflow.ErrorCode).IsEqualTo("llm_tck_video_capacity_exceeded");
        await Assert.That(overflow.Bytes.Length).IsEqualTo(0);
        await Assert.That(runtime.GetAssertionSummary().ErrorsReturned).IsEqualTo(1);
        await Assert.That(runtime.ListVideos(LlmTckCompatibilityTags.OpenAI).Single().VideoId).IsEqualTo(first.VideoId);
        await Assert.That(runtime.ListVideos(LlmTckCompatibilityTags.AzureOpenAI).Count).IsEqualTo(0);
        await Assert.That(runtime.DeleteVideo(LlmTckCompatibilityTags.OpenAI, first.VideoId)).IsTrue();
        await Assert.That(runtime.DeleteVideo(LlmTckCompatibilityTags.OpenAI, first.VideoId)).IsFalse();
        await Assert.That(runtime.StoreVideo(LlmTckCompatibilityTags.AzureOpenAI, video).VideoId).IsEqualTo("video");
        await runtime.ResetAsync();
        await Assert.That(runtime.StoreVideo(LlmTckCompatibilityTags.OpenAI, video).IsSuccess).IsTrue();
        await runtime.ConfigureAsync(configuration);
        await Assert.That(runtime.StoreVideo(LlmTckCompatibilityTags.OpenAI, video).VideoId).IsEqualTo("video");
    }

    [Test]
    public async Task VideoCapacity_ValidatesConfigurationAndHandlesOversizedOrFailedResultsAsync()
    {
        await Assert.That(() => new LlmTckConfigurationBuilder().WithVideoCapacity(-1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckConfigurationBuilder().WithVideoCapacity(1, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckRuntime(new() { MaxVideoJobs = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckRuntime(new() { MaxVideoBytes = -1 })).Throws<ArgumentOutOfRangeException>();
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().WithVideoCapacity(2, 1).Build());
        var video = new LlmTckVideoResult { IsSuccess = true, Bytes = [1, 2] };
        await Assert.That(runtime.StoreVideo("provider", video).IsSuccess).IsFalse();
        await Assert.That(runtime.ListVideos("provider").Count).IsEqualTo(0);
        var failed = video with { IsSuccess = false, StatusCode = 429 };
        await Assert.That(runtime.StoreVideo("provider", failed)).IsEqualTo(failed);
        var disabled = new LlmTckRuntime(new() { MaxVideoJobs = 0 });
        await Assert.That(disabled.StoreVideo("provider", video with { Bytes = [] }).IsSuccess).IsFalse();
    }
}

using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Runtime;

public sealed class LlmTckChatHistoryTests
{
    private const string _prompt = "history-private-evidence";
    private const string _scope = "history-scope";
    private const string _token = "history-owner-token";

    [Test]
    public async Task Continuation_SnapshotsMessagesAndReplacesInstructionsAsync()
    {
        var runtime = CreateRuntime();
        var request = Request();
        request.Messages.Insert(0, new() { Role = "system", Content = "old-instruction" });
        request.Messages.Add(new() { Role = "assistant", ToolCalls = [new() { Id = "call_prior", Name = "read_document", ArgumentsJson = "{}" }] });
        var first = await runtime.CompleteChatAsync(request, _token);
        request.Messages[1] = new() { Role = "user", Content = "mutated" };
        request.Messages.Last().ToolCalls.Clear();
        var next = Request() with
        {
            PreviousResponseId = first.ResponseId,
            Messages = [new() { Role = "developer", Content = "new-instruction" }, new() { Role = "tool", ToolCallId = "call_prior", Content = "read result" }]
        };
        var result = await runtime.CompleteChatAsync(next, _token);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Content).IsEqualTo("second");
        var messages = runtime.GetAssertionSummary().Events.Last().Messages;
        await Assert.That(messages.Any(message => message.Content == _prompt)).IsTrue();
        await Assert.That(messages.Any(message => message.Content is "mutated" or "old-instruction")).IsFalse();
        await Assert.That(messages[0].Content).IsEqualTo("new-instruction");
        await Assert.That(messages.SelectMany(message => message.ToolCalls).Single().Id).IsEqualTo("call_prior");
    }

    [Test]
    [Arguments("missing")]
    [Arguments("namespace")]
    [Arguments("model")]
    [Arguments("token")]
    [Arguments("reset")]
    [Arguments("configure")]
    public async Task ForeignOrExpiredParent_FailsWithoutConsumingAResponseAsync(string change)
    {
        var runtime = CreateRuntime();
        var first = await runtime.CompleteChatAsync(Request(), _token);
        if (change == "reset") { await runtime.ResetAsync(); }
        if (change == "configure") { await runtime.ConfigureAsync(Configuration()); }
        var request = Request() with { PreviousResponseId = first.ResponseId, Messages = [new() { Content = "new input only" }] };
        request = change switch
        {
            "missing" => request with { PreviousResponseId = "resp_missing" },
            "namespace" => request with { HistoryNamespace = "another-scope" },
            "model" => request with { ModelId = LlmTckKnownModelIds.Gpt41 },
            _ => request
        };
        var rejected = await runtime.CompleteChatAsync(request, change == "token" ? "foreign-token" : _token);
        await Assert.That(rejected.StatusCode).IsEqualTo(404);
        await Assert.That(rejected.ErrorCode).IsEqualTo("response_not_found");
        await Assert.That(runtime.GetAssertionSummary().Events.Last().Messages.Any(message => message.Content == _prompt)).IsFalse();
        var accepted = await runtime.CompleteChatAsync(Request() with { StoreResponse = false }, _token);
        await Assert.That(accepted.Content).IsEqualTo(change is "reset" or "configure" ? "first" : "second");
    }

    [Test]
    public async Task StoreFalse_DoesNotRetainAResponseAsync()
    {
        var runtime = CreateRuntime();
        var result = await runtime.CompleteChatAsync(Request() with { StoreResponse = false }, _token);
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.ResponseId).IsNull();
    }

    [Test]
    [Arguments(0, 1000000L)]
    [Arguments(100, 0L)]
    [Arguments(100, 1L)]
    public async Task CapacityRejection_DoesNotConsumeTheFixtureAsync(int count, long bytes)
    {
        var runtime = CreateRuntime(count, bytes);
        var rejected = await runtime.CompleteChatAsync(Request(), _token);
        await Assert.That(rejected.StatusCode).IsEqualTo(409);
        await Assert.That(rejected.ErrorCode).IsEqualTo("llm_tck_response_capacity_exceeded");
        var accepted = await runtime.CompleteChatAsync(Request() with { StoreResponse = false }, _token);
        await Assert.That(accepted.Content).IsEqualTo("first");
    }

    [Test]
    public async Task CapacityAndNamespace_AreValidatedAndResetReleasesCapacityAsync()
    {
        await Assert.That(() => new LlmTckConfigurationBuilder().WithChatResponseCapacity(-1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckConfigurationBuilder().WithChatResponseCapacity(1, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckRuntime(new() { MaxStoredChatResponses = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LlmTckRuntime(new() { MaxStoredChatResponseBytes = -1 })).Throws<ArgumentOutOfRangeException>();
        var options = new LlmTckClientConfigurationBuilder().UseChatResponseCapacity(1, 1000000).Build();
        await Assert.That(options.MaxStoredChatResponses).IsEqualTo(1);
        var runtime = CreateRuntime(1, 1000000);
        await Assert.That((await runtime.CompleteChatAsync(Request() with { HistoryNamespace = null }, _token)).StatusCode).IsEqualTo(400);
        await Assert.That((await runtime.CompleteChatAsync(Request(), _token)).IsSuccess).IsTrue();
        await Assert.That((await runtime.CompleteChatAsync(Request(), _token)).StatusCode).IsEqualTo(409);
        await runtime.ResetAsync();
        await Assert.That((await runtime.CompleteChatAsync(Request(), _token)).IsSuccess).IsTrue();
    }

    private static LlmTckChatRequest Request()
    {
        return new()
        {
            ModelId = LlmTckKnownModelIds.Gpt41Mini,
            HistoryNamespace = _scope,
            StoreResponse = true,
            Messages = [new() { Role = "user", Content = _prompt }]
        };
    }

    private static LlmTckRuntime CreateRuntime(int count = 256, long bytes = 1000000)
    {
        return new(Configuration(count, bytes));
    }

    private static LlmTckConfiguration Configuration(int count = 256, long bytes = 1000000)
    {
        return new LlmTckConfigurationBuilder().WithChatResponseCapacity(count, bytes)
            .AddModel(LlmTckKnownModelIds.Gpt41Mini, LlmTckModelKind.Chat)
            .AddModel(LlmTckKnownModelIds.Gpt41, LlmTckModelKind.Chat)
            .AddChatScenario(_prompt, scenario => scenario.ForModel(LlmTckKnownModelIds.Gpt41Mini)
                .WhenUserContains(_prompt).Responds("first").Responds("second")).Build();
    }
}

using System.Text.Json;
using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class ToolFixtureStateTests
{
    [Test]
    public async Task ParallelCallsAndText_SurviveEveryMapperAndStreamingEventAsync()
    {
        var result = LlmTckChatResult.Success("gpt-4.1-mini", "tools", "checking", ["check", "ing"], new()) with
        { ToolCalls = [new() { Id = "first", Name = "weather", ArgumentsJson = "{\"city\":\"Paris\"}" }, new() { Id = "second", Name = "weather", ArgumentsJson = "{\"city\":\"London\"}" }] };
        var outputs = new object[]
        {
            OpenAiWireMapper.ToChatResponse(result), OpenAiWireMapper.ToResponse(result, "resp_fixture", 0),
            AnthropicWireMapper.ToMessageResponse(result), BedrockWireMapper.ToConverseResponse(result),
            GeminiWireMapper.ToGenerateContentResponse(result, result.Content), OllamaWireMapper.ToChatResponse(result), CohereWireMapper.ToChatResponse(result),
        };
        foreach (var output in outputs)
        {
            var json = JsonSerializer.Serialize(output);
            await Assert.That(json).Contains("checking");
            await Assert.That(json).Contains("Paris");
            await Assert.That(json).Contains("London");
        }
        var responseEvents = JsonSerializer.Serialize(OpenAiWireMapper.ToResponseStreamEvents(result, "resp_fixture", 0).ToArray());
        await Assert.That(responseEvents).Contains("response.output_text.delta");
        await Assert.That(responseEvents).Contains("response.function_call_arguments.delta");
        foreach (var events in new[] { AnthropicWireMapper.ToToolStreamEvents(result, 1), BedrockWireMapper.ToToolStreamEvents(result, 1), CohereWireMapper.ToToolStreamEvents(result) })
        {
            var json = JsonSerializer.Serialize(events.ToArray());
            await Assert.That(json).Contains("Paris");
            await Assert.That(json).Contains("London");
        }
    }

    [Test]
    public async Task ToolHistory_IsSnapshottedAndParticipatesInExactMatchingAsync()
    {
        var call = new LlmTckToolCall { Id = "first", Name = "weather", ArgumentsJson = "{}" };
        var history = new LlmTckMessage { Role = "assistant", ToolCalls = [call] };
        var configuration = new LlmTckConfigurationBuilder().AddChatScenario("history", s => s.WithExactMatch(history).Responds("correct")).Build();
        var runtime = new LlmTckRuntime(configuration);
        history.ToolCalls.Clear();
        configuration.ChatScenarios.Single().Match.Messages.Single().ToolCalls.Clear();
        var wrong = await runtime.CompleteChatAsync(new() { Messages = [history with { ToolCalls = [call with { Id = "other" }] }] });
        await Assert.That(wrong.StatusCode).IsEqualTo(404);
        var right = await runtime.CompleteChatAsync(new() { Messages = [history with { ToolCalls = [call] }] });
        await Assert.That(right.Content).IsEqualTo("correct");
        await Assert.That(right.Usage.InputTokens).IsGreaterThan(0);
        await Assert.That(runtime.GetAssertionSummary().Events.Last().Messages.Single().ToolCalls.Single().Id).IsEqualTo("first");
    }

    [Test]
    public async Task ToolSchemas_ArePartOfPromptCacheIdentityAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("cache", s => s.Responds("one").Responds("two").Responds("three")).Build());
        var request = new LlmTckChatRequest { Messages = [new() { Role = "user", Content = "weather" }], Tools = [new() { Name = "weather" }], PromptCachePolicy = LlmTckPromptCachePolicy.Ollama };
        var first = await runtime.CompleteChatAsync(request);
        await Assert.That(first.Usage.InputTokens).IsGreaterThan(1);
        await Assert.That((await runtime.CompleteChatAsync(request)).Usage.CachedInputTokens).IsGreaterThan(0);
        await Assert.That((await runtime.CompleteChatAsync(request with { Tools = [new() { Name = "other" }] })).Usage.CachedInputTokens).IsEqualTo(0);
    }

    [Test]
    public async Task Builder_ParallelCallsRemainIndependentOfCallerCollectionAsync()
    {
        var calls = new[] { new LlmTckToolCall { Id = "first", Name = "weather" }, new LlmTckToolCall { Id = "second", Name = "weather" } };
        var builder = new LlmTckScenarioBuilder("parallel").CallsTools(calls);
        calls[0] = calls[0] with { Name = "changed" };
        await Assert.That(builder.Build().Responses.Single().ToolCalls.Select(call => call.Name)).IsEquivalentTo(new[] { "weather", "weather" });
        await Assert.That(() => new LlmTckScenarioBuilder("empty").CallsTools()).Throws<ArgumentException>();
    }
}

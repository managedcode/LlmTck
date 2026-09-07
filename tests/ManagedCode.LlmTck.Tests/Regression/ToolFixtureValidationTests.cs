using System.Net;
using System.Text;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;
using ManagedCode.LlmTck.Tests.TestSupport;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class ToolFixtureValidationTests
{
    [Test]
    [Arguments("/openai/v1/chat/completions", "\"tools\":null")]
    [Arguments("/openai/v1/chat/completions", "\"tools\":[null]")]
    [Arguments("/openai/v1/chat/completions", "\"tools\":[{\"type\":\"function\",\"function\":null}]")]
    [Arguments("/openai/v1/chat/completions", "\"tools\":[{\"type\":\"web_search\"}]")]
    [Arguments("/openai/v1/chat/completions", "\"tool_choice\":\"maybe\"")]
    [Arguments("/openai/v1/chat/completions", "\"tool_choice\":{\"type\":\"function\",\"function\":{}}")]
    [Arguments("/openai/v1/chat/completions", "\"response_format\":{\"type\":\"json_schema\"}")]
    [Arguments("/openai/v1/chat/completions", "\"response_format\":{\"type\":\"xml\"}")]
    [Arguments("/openai/v1/responses", "\"text\":{\"format\":{\"type\":\"json_schema\"}}")]
    [Arguments("/anthropic/v1/messages", "\"tools\":[{\"name\":\"weather\"}]")]
    [Arguments("/anthropic/v1/messages", "\"tool_choice\":{\"type\":\"tool\"}")]
    [Arguments("/gemini/v1beta/models/gpt-4.1-mini:generateContent", "\"tools\":[{}]")]
    [Arguments("/gemini/v1beta/models/gpt-4.1-mini:generateContent", "\"toolConfig\":{\"functionCallingConfig\":{\"mode\":\"WRONG\"}}")]
    [Arguments("/ollama/api/chat", "\"format\":\"xml\"")]
    [Arguments("/cohere/v2/chat", "\"tool_choice\":\"MAYBE\"")]
    [Arguments("/bedrock/model/gpt-4.1-mini/converse", "\"toolConfig\":{\"tools\":[null]}")]
    [Arguments("/bedrock/model/gpt-4.1-mini/converse", "\"outputConfig\":{\"textFormat\":{\"type\":\"json_schema\"}}")]
    [Arguments("/openai/v1/chat/completions", "\"messages\":[{\"tool_calls\":[null]}]")]
    [Arguments("/openai/v1/chat/completions", "\"messages\":[{\"tool_calls\":[{\"id\":\"call\",\"function\":{\"name\":\"weather\",\"arguments\":null}}]}]")]
    [Arguments("/openai/v1/responses", "\"input\":[{\"type\":\"function_call\",\"name\":\"weather\"}]")]
    [Arguments("/anthropic/v1/messages", "\"messages\":[{\"content\":[{\"type\":\"tool_use\"}]}]")]
    [Arguments("/gemini/v1beta/models/gpt-4.1-mini:generateContent", "\"contents\":[{\"parts\":[{\"functionCall\":{}}]}]")]
    [Arguments("/bedrock/model/gpt-4.1-mini/converse", "\"messages\":[{\"content\":[{\"toolResult\":{\"toolUseId\":\"call\",\"content\":[null]}}]}]")]
    public async Task InvalidToolOptions_Return400BeforeRuntimeAsync(string path, string fields)
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var http = host.GetTestClient();
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        using var response = await http.PostAsync(path, new StringContent("{" + fields + "}", Encoding.UTF8, "application/json"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("{\"$ref\":\"https://example.com/schema\"}")]
    [Arguments("{\"type\":42}")]
    [Arguments("not json")]
    public async Task InvalidSchema_ReturnsFixtureMismatchWithoutConsumingResponseAsync(string schema)
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("json", s => s.RespondsJson("{}")).Build());
        var request = new LlmTckChatRequest { Messages = [new() { Role = "user", Content = "json" }], RequireJson = true, ResponseSchemaJson = schema };
        await Assert.That((await runtime.CompleteChatAsync(request)).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { ResponseSchemaJson = "{\"type\":\"object\"}" })).Content).IsEqualTo("{}");
    }

    [Test]
    public async Task RequiredAndNoneChoices_DoNotConsumeMismatchedToolFixturesAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("tools", s => s.CallsTool("weather", "{}").Responds("done")).Build());
        var request = new LlmTckChatRequest { Messages = [new() { Role = "user", Content = "weather" }], Tools = [new() { Name = "weather" }] };
        await Assert.That((await runtime.CompleteChatAsync(request with { ToolChoice = LlmTckToolChoice.None })).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { RequiredToolName = "other" })).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { Tools = [] })).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { ToolChoice = LlmTckToolChoice.Required })).ToolCalls.Single().Name).IsEqualTo("weather");
        await Assert.That((await runtime.CompleteChatAsync(request with { ToolChoice = LlmTckToolChoice.Required })).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { ToolChoice = LlmTckToolChoice.None })).Content).IsEqualTo("done");
    }

    [Test]
    public async Task LocalSchemaReferences_AreEvaluatedAndInvalidArgumentsDoNotConsumeAsync()
    {
        var schema = """{"$defs":{"city":{"const":"Paris"}},"type":"object","properties":{"city":{"$ref":"#/$defs/city"}},"required":["city"]}""";
        await Assert.That(LlmTckJsonSchema.Matches("""{"city":"Paris"}""", schema)).IsTrue();
        await Assert.That(LlmTckJsonSchema.Matches("""{"city":"London"}""", schema)).IsFalse();
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("tools", s => s.CallsTool("weather", """{"city":"London"}""")).Build());
        var request = new LlmTckChatRequest { Messages = [new() { Role = "user", Content = "weather" }], Tools = [new() { Name = "weather", ParametersJson = schema }] };
        await Assert.That((await runtime.CompleteChatAsync(request)).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { Tools = [new() { Name = "weather" }] })).ToolCalls.Single().ArgumentsJson).Contains("London");
    }

    [Test]
    public async Task StructuredStream_ValidatesActualConcatenatedContentAsync()
    {
        var runtime = new LlmTckRuntime(new LlmTckConfigurationBuilder().AddChatScenario("json", s => s.Responds("{}", "not json")).Build());
        var request = new LlmTckChatRequest { Messages = [new() { Role = "user", Content = "json" }], RequireJson = true, Stream = true };
        await Assert.That((await runtime.CompleteChatAsync(request)).StatusCode).IsEqualTo(409);
        await Assert.That((await runtime.CompleteChatAsync(request with { RequireJson = false })).Content).IsEqualTo("{}");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class ReviewCapabilityRegressionTests
{
    [Test]
    [Arguments("/openai/v1/chat/completions")]
    [Arguments("/azure-openai/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2024-10-21")]
    [Arguments("/azure-openai/openai/v1/chat/completions")]
    [Arguments("/microsoft-foundry/chat/completions?api-version=2024-05-01-preview")]
    [Arguments("/microsoft-foundry/openai/v1/chat/completions")]
    [Arguments("/groq/openai/v1/chat/completions")]
    [Arguments("/mistral/v1/chat/completions")]
    [Arguments("/openrouter/api/v1/chat/completions")]
    [Arguments("/deepseek/v1/chat/completions")]
    [Arguments("/perplexity/v1/sonar")]
    [Arguments("/anthropic/v1/messages")]
    [Arguments("/ollama/api/chat")]
    [Arguments("/cohere/v2/chat")]
    [Arguments("/gemini/v1beta/models/gpt-4.1-mini:generateContent")]
    [Arguments("/bedrock/model/gpt-4.1-mini/converse")]
    [Arguments("/bedrock/model/gpt-4.1-mini/invoke")]
    [Arguments("/openai/v1/responses")]
    public async Task LegacyFunctions_ReturnExplicitErrorWithoutConsumingFixtureAsync(string path)
    {
        using var host = await LlmTckTestHost.StartAsync(b => b.AddChatScenario("queue", s => s.Responds("blue whale")));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var body = JsonNode.Parse("""{"model":"gpt-4.1-mini","max_tokens":10,"messages":[{"role":"user","content":"whale"}],"input":"whale","contents":[{"parts":[{"text":"whale"}]}]}""")!.AsObject();
        body["functions"] = JsonNode.Parse("""[{"name":"lookup_whale"}]""");
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var runtime = host.Services.GetRequiredService<ILlmTckRuntime>();
        await Assert.That(runtime.GetAssertionSummary().TotalEvents).IsEqualTo(0);
    }

    [Test]
    [Arguments("{\"tools\":[]}", false)]
    [Arguments("{\"tool_choice\":\"none\"}", false)]
    [Arguments("{\"tool_choice\":\"required\"}", true)]
    [Arguments("{\"response_format\":{\"type\":\"text\"}}", false)]
    [Arguments("{\"response_format\":{\"type\":7}}", true)]
    [Arguments("{\"toolConfig\":{}}", true)]
    [Arguments("{\"generationConfig\":{\"responseJsonSchema\":{}}}", true)]
    public async Task CapabilityPolicy_DistinguishesTextDefaultsFromUnsupportedFeaturesAsync(string json, bool unsupported)
    {
        await Assert.That(LlmTckRequestPolicy.HasUnsupportedChatFeatures(JsonSerializer.Deserialize<JsonElement>(json))).IsEqualTo(unsupported);
    }
}

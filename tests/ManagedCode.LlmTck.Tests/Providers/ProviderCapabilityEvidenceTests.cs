using System.Net;
using System.Text;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.Regression;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class ProviderCapabilityEvidenceTests
{
    [Test]
    [Arguments(LlmTckCompatibilityTags.OpenAI)]
    [Arguments(LlmTckCompatibilityTags.AzureOpenAI)]
    [Arguments(LlmTckCompatibilityTags.MicrosoftFoundry)]
    [Arguments(LlmTckCompatibilityTags.Anthropic)]
    [Arguments(LlmTckCompatibilityTags.Gemini)]
    [Arguments(LlmTckCompatibilityTags.Groq)]
    [Arguments(LlmTckCompatibilityTags.Mistral)]
    [Arguments(LlmTckCompatibilityTags.Ollama)]
    [Arguments(LlmTckCompatibilityTags.Cohere)]
    [Arguments(LlmTckCompatibilityTags.Bedrock)]
    [Arguments(LlmTckCompatibilityTags.OpenRouter)]
    [Arguments(LlmTckCompatibilityTags.DeepSeek)]
    [Arguments(LlmTckCompatibilityTags.Perplexity)]
    public async Task ClaimedToolAndSchemaCases_ExecutePositiveAndNegativeHttpEvidenceAsync(string provider)
    {
        var profile = ProviderApiContractTests.GetProfiles().Single(item => item.Id == provider);
        foreach (var operation in profile.ApiContract.Operations.Where(operation => operation.ImplementedByHosting))
        {
            foreach (var capability in operation.Capabilities.Where(capability => capability is LlmTckProviderCapability.Tools or LlmTckProviderCapability.StructuredOutput))
            {
                var streamingOnly = operation.Id is LlmTckProviderOperationIds.Gemini.ModelsStreamGenerateContent or LlmTckProviderOperationIds.Bedrock.ConverseStream;
                var modes = streamingOnly ? new[] { true } : operation.SupportsStreaming ? [false, true] : [false];
                foreach (var stream in modes)
                {
                    await VerifyCaseAsync(provider, operation, capability, stream);
                }
            }
        }
    }

    private static async Task VerifyCaseAsync(string provider, LlmTckProviderOperation operation, LlmTckProviderCapability capability, bool stream)
    {
        var caseId = $"{provider}/{operation.Id}/{capability}/{(stream ? "stream" : "json")}";
        var tools = capability == LlmTckProviderCapability.Tools;
        const string answer = "{\"city\":\"Paris\"}";
        using var host = await LlmTckTestHost.StartAsync(b => b.AddChatScenario(caseId, s =>
        {
            if (tools) { s.CallsTool("weather", answer); }
            else { s.RespondsJson(answer); }
        }));
        using var http = host.GetTestClient();
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var format = provider switch
        {
            LlmTckCompatibilityTags.Anthropic => 2,
            LlmTckCompatibilityTags.Gemini => 3,
            LlmTckCompatibilityTags.Ollama => 4,
            LlmTckCompatibilityTags.Cohere => 5,
            LlmTckCompatibilityTags.Bedrock => 6,
            _ => operation.Id is LlmTckProviderOperationIds.OpenAI.ResponsesCreate or LlmTckProviderOperationIds.AzureOpenAI.V1ResponsesCreate ? 1 : 0,
        };
        var body = ToolFixtureEndpointTests.Request(format, tools);
        body["stream"] = stream;
        var path = operation.Path.Replace("{model}", "gpt-4.1-mini", StringComparison.Ordinal)
            .Replace("{modelId}", "gpt-4.1-mini", StringComparison.Ordinal)
            .Replace("{deployment}", "gpt-4.1-mini", StringComparison.Ordinal);
        if (operation.ApiVersion is { } version && version != "v1") { path += "?api-version=" + version; }
        if (provider == LlmTckCompatibilityTags.Gemini && stream) { path += "?alt=sse"; }
        var invalid = body.ToJsonString().Replace("Paris", "London", StringComparison.Ordinal);
        using var rejected = await http.PostAsync(path, new StringContent(invalid, Encoding.UTF8, "application/json"));
        await Assert.That((caseId + "/reject", rejected.StatusCode)).IsEqualTo((caseId + "/reject", HttpStatusCode.Conflict));
        using var accepted = await http.PostAsync(path, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        await Assert.That((caseId + "/accept", accepted.StatusCode)).IsEqualTo((caseId + "/accept", HttpStatusCode.OK));
        var content = await accepted.Content.ReadAsStringAsync();
        await Assert.That(content).Contains(tools ? "weather" : "Paris");
        if (stream)
        {
            var marker = format switch { 1 => "response.completed", 2 => "message_stop", 3 => "data:", 4 => "\"done\":true", 5 => "message-end", 6 => "messageStop", _ => "[DONE]" };
            await Assert.That(content).Contains(marker);
        }
        var summary = host.Services.GetRequiredService<ILlmTckRuntime>().GetAssertionSummary();
        await Assert.That(summary.ErrorsReturned).IsEqualTo(1);
        await Assert.That(summary.Matched).IsEqualTo(1);
        await Assert.That(summary.Events.All(entry => entry.ScenarioId == caseId)).IsTrue();
    }

    [Test]
    public async Task EvidenceGuard_RequiresExecutableProviderAndEdgeCasesAsync()
    {
        var method = typeof(ProviderCapabilityEvidenceTests).GetMethod(nameof(ClaimedToolAndSchemaCases_ExecutePositiveAndNegativeHttpEvidenceAsync))!;
        await Assert.That(method.IsDefined(typeof(TestAttribute), false)).IsTrue();
        var providers = ArgumentRows(method).Select(row => (string)row[0].Value!).Order().ToArray();
        await Assert.That(providers).IsEquivalentTo(ProviderApiContractTests.GetProfiles().Select(profile => profile.Id));
        var schema = typeof(FollowUpEndpointTests).GetMethod(nameof(FollowUpEndpointTests.GeminiSchema_ValidatesNullableNestedAlternativesAndStringLimitsAsync))!;
        await Assert.That(schema.IsDefined(typeof(TestAttribute), false)).IsTrue();
        await Assert.That(ArgumentRows(schema).Select(row => (string)row[0].Value!)).IsEquivalentTo(new[] { "nullable", "string-limits", "nested-anyof" });
        var parallel = typeof(FollowUpEndpointTests).GetMethod(nameof(FollowUpEndpointTests.ParallelTools_RespectRequestLimitAndPreserveQueueAsync))!;
        await Assert.That(parallel.IsDefined(typeof(TestAttribute), false)).IsTrue();
        await Assert.That(ArgumentRows(parallel).Select(row => ((int)row[1].Value!, (bool)row[2].Value!)))
            .IsEquivalentTo(new[] { (0, false), (0, true), (1, false), (1, true), (2, false), (2, true) });
    }

    private static IEnumerable<IList<System.Reflection.CustomAttributeTypedArgument>> ArgumentRows(System.Reflection.MethodInfo method)
    {
        return method.GetCustomAttributesData().Where(attribute => attribute.AttributeType == typeof(ArgumentsAttribute))
            .Select(attribute => (IList<System.Reflection.CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!);
    }
}

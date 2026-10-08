using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Decisions;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.TestSupport;

namespace ManagedCode.LlmTck.Tests.Decisions;

public sealed class DecisionRuntimeTests
{
    private const string _model = "jev-latest";

    [Test]
    [Arguments("unknown", 404)]
    [Arguments("unmatched", 404)]
    [Arguments("error", 503)]
    [Arguments("mismatch", 409)]
    [Arguments("score-key", 409)]
    [Arguments("score-high", 409)]
    [Arguments("wrong-input", 404)]
    public async Task DecisionFailures_AreObservableAsync(string kind, int status)
    {
        var builder = new LlmTckConfigurationBuilder();
        if (kind != "unknown") { builder.AddModel(_model, LlmTckModelKind.Decision); }
        var scenario = DecisionEndpointTests.Scenario(_model);
        if (kind == "error") { scenario = scenario with { Error = new() { StatusCode = status, Code = "configured", Message = "configured outage" } }; }
        if (kind == "mismatch") { scenario.Answers.Remove("binary"); }
        if (kind == "score-key") { scenario.Answers["score"] = scenario.Answers["score"] with { Probabilities = new() { ["invalid"] = 0.5 } }; }
        if (kind == "score-high") { scenario.Answers["score"] = scenario.Answers["score"] with { Score = 100 }; }
        if (kind == "wrong-input") { scenario = scenario with { ExpectedInput = "other" }; }
        if (kind != "unmatched") { builder.AddDecisionScenario(scenario); }
        var runtime = new LlmTckRuntime(builder.Build());
        var result = await runtime.DecideAsync(Request());
        await Assert.That(result.StatusCode).IsEqualTo(status);
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(runtime.GetAssertionSummary().Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DecisionFixtures_CopyInputsAndReturnedAnswersAsync()
    {
        var scenario = DecisionEndpointTests.Scenario(_model);
        var builder = new LlmTckConfigurationBuilder().AddModel(_model, LlmTckModelKind.Decision).AddDecisionScenario(scenario);
        scenario.Answers["choice"].Probabilities["opaque-id"] = 0;
        var runtime = new LlmTckRuntime(builder.Build());
        var first = await runtime.DecideAsync(Request()); first.Answers["choice"].Probabilities["opaque-id"] = 0;
        var second = await runtime.DecideAsync(Request());
        await Assert.That(second.Answers["choice"].Probabilities["opaque-id"]).IsEqualTo(0.95);
        await runtime.ResetAsync();
        await Assert.That((await runtime.DecideAsync(Request())).IsSuccess).IsTrue();
        using var active = ((ILlmTckRuntimeRequestScope)runtime).BeginRequest("obsolete");
        await runtime.ResetAsync();
        await Assert.That((await runtime.DecideAsync(Request())).ErrorCode).IsEqualTo("llm_tck_request_superseded");
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    public async Task DecisionFixtureValidation_RejectsInvalidProbabilityAsync(double value)
    {
        var scenario = DecisionEndpointTests.Scenario(_model); scenario.Answers["binary"] = new() { Probability = value };
        await Assert.That(() => new LlmTckConfigurationBuilder().AddDecisionScenario(scenario)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments("score")]
    [Arguments("choice")]
    [Arguments("kind")]
    public async Task DecisionFixtureValidation_RejectsMissingValuesAsync(string kind)
    {
        var scenario = DecisionEndpointTests.Scenario(_model);
        scenario.Answers["binary"] = new() { Kind = kind switch { "score" => LlmTckDecisionKind.Score, "choice" => LlmTckDecisionKind.Choice, _ => (LlmTckDecisionKind)100 } };
        await Assert.That(() => new LlmTckConfigurationBuilder().AddDecisionScenario(scenario)).Throws<ArgumentException>();
    }

    [Test]
    public async Task OpenAiDecisions_EmitNativeRefusalAnswersAsync()
    {
        var scenario = DecisionEndpointTests.Scenario("gpt-6-luna"); scenario.Answers["0"] = new() { Refused = true };
        using var host = await LlmTckTestHost.StartAsync(builder => builder.AddModel("gpt-6-luna", LlmTckModelKind.Decision).AddDecisionScenario(scenario));
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor("gpt-6-luna"), DecisionEndpointTests.Body("gpt-6-luna"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(payload.GetProperty("answers")[0].GetProperty("type").GetString()).IsEqualTo("refusal");
        await Assert.That(payload.GetProperty("answers")[0].TryGetProperty("probability", out _)).IsFalse();
    }

    private static LlmTckDecisionRequest Request()
    {
        return new()
        {
            ModelId = _model,
            Input = "evidence",
            Questions =
        [new() { Name = "binary" }, new() { Name = "choice", Kind = LlmTckDecisionKind.Choice, Options = ["opaque-id", "other-id"] },
            new() { Name = "score", Kind = LlmTckDecisionKind.Score, Options = ["low", "middle", "high"] }],
        };
    }
}

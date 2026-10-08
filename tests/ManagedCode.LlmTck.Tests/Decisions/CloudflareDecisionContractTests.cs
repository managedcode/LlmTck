using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCode.LlmTck.Cloudflare;
using ManagedCode.LlmTck.Decisions;
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Decisions;

public sealed class CloudflareDecisionContractTests
{
    [Test]
    [Arguments("clef", "image/png", false)]
    [Arguments("clef", "image/jpeg", true)]
    [Arguments("clef", "image/webp", false)]
    [Arguments("clef-flash", "image/png", true)]
    [Arguments("clef-flash", "image/jpeg", false)]
    [Arguments("clef-flash", "image/webp", true)]
    public async Task Images_PreserveNativeRepresentationScenarioInputAndRuntimeEvidenceAsync(string model, string contentType, bool embedded)
    {
        var body = CloudflareDecisionTestFixtures.Body(model);
        body["images"] = new JsonArray(CloudflareDecisionTestFixtures.Image(contentType, embedded));
        var expectedInput = JsonSerializer.Serialize(new
        {
            state = JsonSerializer.SerializeToElement(body["state"]),
            images = JsonSerializer.SerializeToElement(body["images"]),
        });
        var scenario = CloudflareDecisionTestFixtures.Scenario(model) with { ExpectedInput = expectedInput };
        using var host = await LlmTckTestHost.StartAsync(builder => builder.AddClef().AddClefFlash().AddDecisionScenario(scenario));
        using var client = host.GetTestClient();

        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor(model), body);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var summary = host.Services.GetRequiredService<ILlmTckRuntime>().GetAssertionSummary();
        await Assert.That(summary.Matched).IsEqualTo(1);
        var neutral = JsonSerializer.Deserialize<LlmTckDecisionRequest>(summary.Events.Single().Request!)!;
        await Assert.That(neutral.Input).IsEqualTo(expectedInput);
        var trace = host.Services.GetRequiredService<ILlmTckProviderHttpTraceStore>().GetSnapshot().Single();
        var native = JsonSerializer.Deserialize<CloudflareDecisionRequest>(trace.RequestPreview!)!;
        await Assert.That(native.Images!.Count).IsEqualTo(1);
        await Assert.That(JsonSerializer.SerializeToElement(native.Images).GetRawText())
            .IsEqualTo(JsonSerializer.SerializeToElement(body["images"]).GetRawText());
    }

    [Test]
    [Arguments("clef")]
    [Arguments("clef-flash")]
    public async Task ModelSelector_AllowsDocumentedSurroundingWhitespaceAsync(string model)
    {
        var body = CloudflareDecisionTestFixtures.Body(model); body["model"] = " \t" + model + "\r\n ";
        using var host = await LlmTckTestHost.StartAsync(builder => builder.AddClef().AddClefFlash()
            .AddDecisionScenario(CloudflareDecisionTestFixtures.Scenario(model)));
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor(model), body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<CloudflareDecisionResponse>();
        await Assert.That(payload!.Result!.Model).IsEqualTo(model);
    }

    [Test]
    public async Task Instructions_AcceptWhitespaceAsNonemptyNativeContentAsync()
    {
        var body = CloudflareDecisionTestFixtures.Body("clef"); body["questions"]!["binary"]!["instructions"] = " \t\n";
        using var host = await LlmTckTestHost.StartAsync(builder => builder.AddClef()
            .AddDecisionScenario(CloudflareDecisionTestFixtures.Scenario("clef")));
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor("clef"), body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task PredicateCriteria_RejectsExplicitNullWithNativeErrorEnvelopeAsync()
    {
        var body = CloudflareDecisionTestFixtures.Body("clef"); body["questions"]!["binary"]!["criteria"] = null;
        using var host = await LlmTckTestHost.StartAsync(); using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor("clef"), body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadFromJsonAsync<CloudflareDecisionResponse>();
        await Assert.That(payload!.Success).IsFalse();
        await Assert.That(payload.Errors.Single().Code).IsEqualTo(CloudflareDecisionErrors.InvalidData);
    }

    [Test]
    public async Task Choice_AllowsWhitespaceOpaqueValuesAndPreservesNativeConfidenceAsync()
    {
        var body = CloudflareDecisionTestFixtures.Body("clef");
        body["questions"] = new JsonObject
        {
            ["choice"] = new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] = "Select the evidence.",
                ["criteria"] = new JsonObject { [" "] = "Whitespace value", ["Other"] = "Other value" },
            },
        };
        var scenario = CloudflareDecisionTestFixtures.Scenario("clef") with
        {
            Answers = new(StringComparer.Ordinal)
            {
                ["choice"] = new()
                {
                    Kind = LlmTckDecisionKind.Choice,
                    Choice = " ",
                    Confidence = 0.75,
                    Probabilities = new(StringComparer.Ordinal) { [" "] = 0.75, ["Other"] = 0.25 },
                },
            },
        };
        using var host = await LlmTckTestHost.StartAsync(builder => builder.AddClef().AddDecisionScenario(scenario));
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor("clef"), body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<CloudflareDecisionResponse>();
        var answer = payload!.Result!.Answers["choice"];
        await Assert.That(answer.Choice).IsEqualTo(" ");
        await Assert.That(answer.Confidence).IsEqualTo(0.75);
        await Assert.That(answer.Probabilities![" "]).IsEqualTo(0.75);
        await Assert.That(answer.Probabilities["Other"]).IsEqualTo(0.25);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Identifiers_RejectsEmptyQuestionOrChoiceValueWithNativeErrorAsync(bool emptyQuestion)
    {
        var body = CloudflareDecisionTestFixtures.Body("clef");
        body["questions"] = emptyQuestion
            ? new JsonObject { [""] = new JsonObject { ["type"] = "noul", ["instructions"] = "Does the evidence suffice?" } }
            : new JsonObject
            {
                ["choice"] = new JsonObject
                {
                    ["type"] = "choice",
                    ["instructions"] = "Select the evidence.",
                    ["criteria"] = new JsonObject { [""] = "Empty value", ["Other"] = "Other value" },
                },
            };
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(DecisionEndpointTests.PathFor("clef"), body);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadFromJsonAsync<CloudflareDecisionResponse>();
        await Assert.That(payload!.Success).IsFalse();
        await Assert.That(payload.Errors.Single().Code).IsEqualTo(CloudflareDecisionErrors.InvalidData);
    }
}

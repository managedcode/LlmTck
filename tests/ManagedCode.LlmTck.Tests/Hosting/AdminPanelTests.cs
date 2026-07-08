using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;

namespace ManagedCode.LlmTck.Tests.Hosting;

public sealed class AdminPanelTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task AdminPanel_ServesStyledHtmlShellAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync(LlmTckControlRoutes.Admin);
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(body).Contains("<!doctype html>");
        await Assert.That(body).Contains("LLM&nbsp;TCK");
        await Assert.That(body).Contains(LlmTckControlRoutes.Models);
        await Assert.That(body).Contains(LlmTckControlRoutes.Assertions);
        await Assert.That(body).Contains("Total tokens");
        await Assert.That(body).Contains("tokens:");
    }

    [Test]
    public async Task AdminPanel_StaysReachableWhenBearerTokenIsRequiredAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.RequireBearerToken("test-key"));
        using var client = host.GetTestClient();

        var page = await client.GetAsync(LlmTckControlRoutes.Admin);
        var protectedModels = await client.GetAsync(LlmTckControlRoutes.Models);

        // The shell renders without a token so operators can enter one; the data
        // endpoints it calls remain protected.
        page.EnsureSuccessStatusCode();
        await Assert.That(protectedModels.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task LegacyDoubleUnderscoreControlRoutes_AreNotMappedAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var adminPage = await client.GetAsync("/__llm-tck");
        var models = await client.GetAsync("/__llm-tck/models");
        var assertions = await client.GetAsync("/__llm-tck/assertions");
        var reset = await client.PostAsync("/__llm-tck/reset", content: null);

        await Assert.That(adminPage.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(models.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(assertions.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(reset.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RuntimeEvents_CaptureIncomingRequestAndModelResponseAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "capture-scenario",
                scenario => scenario
                    .ForModel("llm-tck-chat")
                    .WhenUserContains("color")
                    .Responds("blue whale")
            ));
        using var client = host.GetTestClient();

        var chat = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new
            {
                model = "llm-tck-chat",
                messages = new[] { new { role = "user", content = "what color is the whale" } },
            },
            _jsonOptions
        );
        chat.EnsureSuccessStatusCode();

        var summary = await client.GetFromJsonAsync<JsonElement>(
            LlmTckControlRoutes.Assertions,
            _jsonOptions
        );
        var events = summary.GetProperty("events");
        var lastEvent = events[events.GetArrayLength() - 1];

        // Operators must be able to see exactly what was asked and what the model answered.
        await Assert.That(lastEvent.GetProperty("request").GetString())
            .Contains("user: what color is the whale");
        await Assert.That(lastEvent.GetProperty("response").GetString()).IsEqualTo("blue whale");
        await Assert.That(summary.GetProperty("inputTokens").GetInt32()).IsEqualTo(5);
        await Assert.That(summary.GetProperty("outputTokens").GetInt32()).IsEqualTo(2);
        await Assert.That(summary.GetProperty("totalTokens").GetInt32()).IsEqualTo(7);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("inputTokens").GetInt32())
            .IsEqualTo(5);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("outputTokens").GetInt32())
            .IsEqualTo(2);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("totalTokens").GetInt32())
            .IsEqualTo(7);
    }
}

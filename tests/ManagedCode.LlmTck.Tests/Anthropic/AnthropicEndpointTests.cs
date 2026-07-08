using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;

namespace ManagedCode.LlmTck.Tests.Anthropic;

public sealed class AnthropicEndpointTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task MessagesEndpoint_ReturnsAnthropicMessageShapeAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .RequireBearerToken("test-key")
                .AddModel("claude-test", LlmTckModelKind.Chat)
                .AddChatScenario(
                    "anthropic-blue-whale",
                    scenario => scenario
                        .ForModel("claude-test")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale")
                ));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-api-key", "test-key");
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.PostAsJsonAsync(
            "/v1/messages",
            new
            {
                model = "claude-test",
                max_tokens = 256,
                messages = new[] { new { role = "user", content = "largest animal?" } },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        await Assert.That(payload.GetProperty("type").GetString()).IsEqualTo("message");
        await Assert.That(payload.GetProperty("role").GetString()).IsEqualTo("assistant");
        await Assert.That(payload.GetProperty("model").GetString()).IsEqualTo("claude-test");
        await Assert.That(payload.GetProperty("stop_reason").GetString()).IsEqualTo("end_turn");
        await Assert.That(payload.GetProperty("content")[0].GetProperty("type").GetString())
            .IsEqualTo("text");
        await Assert.That(payload.GetProperty("content")[0].GetProperty("text").GetString())
            .IsEqualTo("blue whale");
        await Assert.That(payload.GetProperty("usage").GetProperty("input_tokens").GetInt32())
            .IsGreaterThan(0);
        await Assert.That(payload.GetProperty("usage").GetProperty("output_tokens").GetInt32())
            .IsGreaterThan(0);
    }

    [Test]
    public async Task MessagesEndpoint_StreamsAnthropicServerSentEventsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .AddModel("claude-test", LlmTckModelKind.Chat)
                .AddChatScenario(
                    "anthropic-stream",
                    scenario => scenario
                        .ForModel("claude-test")
                        .WhenUserContains("stream")
                        .Responds("blue whale", "blue ", "whale")
                ));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.PostAsJsonAsync(
            "/v1/messages",
            new
            {
                model = "claude-test",
                max_tokens = 256,
                stream = true,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new[] { new { type = "text", text = "stream response" } },
                    },
                },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("text/event-stream");
        await Assert.That(body).Contains("event: message_start");
        await Assert.That(body).Contains("event: content_block_start");
        await Assert.That(body).Contains("event: content_block_delta");
        await Assert.That(body).Contains("event: content_block_stop");
        await Assert.That(body).Contains("event: message_delta");
        await Assert.That(body).Contains("event: message_stop");
        await Assert.That(body).Contains("\"usage\":{\"input_tokens\":");
        await Assert.That(body).Contains("\"usage\":{\"output_tokens\":2}");
        await Assert.That(body.Contains("\"input_tokens\":0")).IsFalse();
        await Assert.That(body).Contains("\"type\":\"text_delta\"");
        await Assert.That(body).Contains("blue ");
        await Assert.That(body).Contains("whale");
    }

    [Test]
    public async Task MessagesEndpoint_RequiresAnthropicVersionHeaderAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/messages",
            new
            {
                model = "llm-tck-chat",
                max_tokens = 256,
                messages = new[] { new { role = "user", content = "hello" } },
            },
            _jsonOptions
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        await Assert.That(payload.GetProperty("type").GetString()).IsEqualTo("error");
        await Assert.That(payload.GetProperty("error").GetProperty("type").GetString())
            .IsEqualTo("invalid_request_error");
    }

    [Test]
    public async Task MessagesEndpoint_AcceptsAnthropicApiKeyHeaderAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .RequireBearerToken("test-key")
                .AddChatScenario(
                    "anthropic-api-key",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("api key")
                        .Responds("accepted")
                ));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-api-key", "test-key");
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.PostAsJsonAsync(
            "/v1/messages",
            new
            {
                model = "llm-tck-chat",
                max_tokens = 256,
                messages = new[] { new { role = "user", content = "api key" } },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
    }
}

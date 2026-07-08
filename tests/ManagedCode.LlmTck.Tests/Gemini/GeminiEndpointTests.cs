using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;

namespace ManagedCode.LlmTck.Tests.Gemini;

public sealed class GeminiEndpointTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task GenerateContentEndpoint_ReturnsGeminiCandidateShapeAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .AddModel("gemini-test", LlmTckModelKind.Chat)
                .AddChatScenario(
                    "gemini-rings",
                    scenario => scenario
                        .ForModel("gemini-test")
                        .WhenUserContains("planet with rings")
                        .Responds("Saturn", "Sat", "urn")
                ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1beta/models/gemini-test:generateContent?key=test-key",
            new
            {
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = "planet with rings" },
                            new Dictionary<string, object?>
                            {
                                ["inline_data"] = new
                                {
                                    mime_type = "image/png",
                                    data = "AA==",
                                },
                            },
                        },
                    },
                },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var candidate = payload.GetProperty("candidates")[0];

        await Assert.That(candidate.GetProperty("content").GetProperty("role").GetString())
            .IsEqualTo("model");
        await Assert.That(
                candidate
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
            )
            .IsEqualTo("Saturn");
        await Assert.That(candidate.GetProperty("finishReason").GetString()).IsEqualTo("STOP");
        await Assert.That(payload.GetProperty("modelVersion").GetString()).IsEqualTo("gemini-test");
        await Assert.That(payload.GetProperty("usageMetadata").GetProperty("totalTokenCount").GetInt32())
            .IsGreaterThan(0);
    }

    [Test]
    public async Task StreamGenerateContentEndpoint_ReturnsGeminiServerSentEventsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .AddModel("gemini-test", LlmTckModelKind.Chat)
                .AddChatScenario(
                    "gemini-stream",
                    scenario => scenario
                        .ForModel("gemini-test")
                        .WhenUserContains("stream")
                        .Responds("red blue", "red ", "blue")
                ));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-goog-api-key", "test-key");

        var response = await client.PostAsJsonAsync(
            "/v1beta/models/gemini-test:streamGenerateContent?alt=sse",
            new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = "stream response" },
                        },
                    },
                },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("text/event-stream");
        await Assert.That(body).Contains("data: {");
        await Assert.That(body).Contains("\"candidates\"");
        await Assert.That(body).Contains("red ");
        await Assert.That(body).Contains("blue");
        await Assert.That(body).Contains("\"finishReason\":\"STOP\"");
    }

    [Test]
    public async Task EmbedContentEndpoint_ReturnsGeminiEmbeddingShapeAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .AddModel("gemini-embedding-001", LlmTckModelKind.Embedding)
                .WithDefaultEmbeddingVector(0.125f, 0.25f));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1beta/models/gemini-embedding-001:embedContent",
            new
            {
                model = "models/gemini-embedding-001",
                content = new
                {
                    parts = new[]
                    {
                        new { text = "What is the meaning of life?" },
                    },
                },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        await Assert.That(payload.GetProperty("embedding").GetProperty("values").GetArrayLength())
            .IsEqualTo(2);
        await Assert.That(
                payload.GetProperty("embedding").GetProperty("values")[0].GetSingle()
            )
            .IsEqualTo(0.125f);
        await Assert.That(payload.GetProperty("usageMetadata").GetProperty("totalTokenCount").GetInt32())
            .IsEqualTo(1);
    }

    [Test]
    public async Task PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .AddModel("veo-3.1-generate-preview", LlmTckModelKind.Video)
                .WithDefaultVideo([0, 0, 0, 24, 102, 116, 121, 112], "video/mp4"));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-goog-api-key", "test-key");

        var startResponse = await client.PostAsJsonAsync(
            "/v1beta/models/veo-3.1-generate-preview:predictLongRunning",
            new
            {
                instances = new[]
                {
                    new
                    {
                        prompt = "a deterministic tck video",
                    },
                },
                parameters = new
                {
                    aspectRatio = "16:9",
                },
            },
            _jsonOptions
        );

        startResponse.EnsureSuccessStatusCode();
        var startPayload = await startResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var operationName = startPayload.GetProperty("name").GetString();
        var hasStartResponse = startPayload.TryGetProperty("response", out _);

        await Assert.That(operationName)
            .IsEqualTo("models/veo-3.1-generate-preview/operations/gen_llm_tck");
        await Assert.That(startPayload.GetProperty("done").GetBoolean()).IsFalse();
        await Assert.That(hasStartResponse).IsFalse();

        var pollResponse = await client.GetAsync($"/v1beta/{operationName}?key=test-key");

        pollResponse.EnsureSuccessStatusCode();
        var pollPayload = await pollResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var video = pollPayload
            .GetProperty("response")
            .GetProperty("generateVideoResponse")
            .GetProperty("generatedSamples")[0]
            .GetProperty("video");
        var fileUri = video.GetProperty("uri").GetString();

        await Assert.That(pollPayload.GetProperty("done").GetBoolean()).IsTrue();
        await Assert.That(video.GetProperty("mimeType").GetString()).IsEqualTo("video/mp4");
        await Assert.That(fileUri).IsNotNull();

        var fileMetadataResponse = await client.GetAsync(new Uri(fileUri!).PathAndQuery);

        fileMetadataResponse.EnsureSuccessStatusCode();
        var filePayload = await fileMetadataResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var downloadUri = filePayload.GetProperty("downloadUri").GetString();

        await Assert.That(filePayload.GetProperty("name").GetString()).IsEqualTo("files/video_llm_tck");
        await Assert.That(filePayload.GetProperty("state").GetString()).IsEqualTo("ACTIVE");
        await Assert.That(filePayload.GetProperty("source").GetString()).IsEqualTo("GENERATED");
        await Assert.That(
                filePayload.GetProperty("videoMetadata").GetProperty("videoDuration").GetString()
            )
            .IsEqualTo("4s");

        var mediaResponse = await client.GetAsync(new Uri(downloadUri!).PathAndQuery);

        mediaResponse.EnsureSuccessStatusCode();
        await Assert.That(mediaResponse.Content.Headers.ContentType?.MediaType).IsEqualTo("video/mp4");
        await Assert.That(await mediaResponse.Content.ReadAsByteArrayAsync())
            .IsEquivalentTo((byte[])[0, 0, 0, 24, 102, 116, 121, 112]);
    }
}

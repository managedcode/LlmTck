using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;

namespace ManagedCode.LlmTck.Tests.OpenAI;

public sealed class OpenAiEndpointTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task OpenAiEndpoints_ExposeModelsAndAllModalitiesAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .RequireBearerToken("test-key")
                .AddModel("chat-model", LlmTckModelKind.Chat)
                .AddModel("embedding-model", LlmTckModelKind.Embedding)
                .AddModel("image-model", LlmTckModelKind.Image)
                .AddModel("audio-model", LlmTckModelKind.Audio)
                .WithDefaultEmbeddingVector(0.25f, 0.5f)
                .AddChatScenario(
                    "endpoint-blue-whale",
                    scenario => scenario
                        .ForModel("chat-model")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale", "blue ", "whale")
                ));
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");

        var models = await client.GetFromJsonAsync<JsonElement>("/v1/models", _jsonOptions);
        var chat = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new
            {
                model = "chat-model",
                messages = new[] { new { role = "user", content = "largest animal?" } },
            },
            _jsonOptions
        );
        var embedding = await client.PostAsJsonAsync(
            "/v1/embeddings",
            new { model = "embedding-model", input = new[] { "alpha", "beta" } },
            _jsonOptions
        );
        var image = await client.PostAsJsonAsync(
            "/v1/images/generations",
            new { model = "image-model", prompt = "draw a blue compatibility marker" },
            _jsonOptions
        );
        var audio = await client.PostAsJsonAsync(
            "/v1/audio/speech",
            new { model = "audio-model", input = "speak this fixture" },
            _jsonOptions
        );

        chat.EnsureSuccessStatusCode();
        embedding.EnsureSuccessStatusCode();
        image.EnsureSuccessStatusCode();
        audio.EnsureSuccessStatusCode();

        var chatJson = await chat.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var embeddingJson = await embedding.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var imageJson = await image.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var audioBytes = await audio.Content.ReadAsByteArrayAsync();

        await Assert.That(models.GetProperty("data").GetArrayLength()).IsGreaterThanOrEqualTo(4);
        await Assert.That(
                chatJson
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
            )
            .IsEqualTo("blue whale");
        await Assert.That(embeddingJson.GetProperty("data").GetArrayLength()).IsEqualTo(2);
        await Assert.That(
                embeddingJson.GetProperty("data")[0].GetProperty("embedding").GetArrayLength()
            )
            .IsEqualTo(2);
        await Assert.That(
                imageJson.GetProperty("data")[0].GetProperty("b64_json").GetString()
            )
            .IsNotNull()
            .And
            .IsNotEmpty();
        await Assert.That(audio.Content.Headers.ContentType?.MediaType).IsEqualTo("audio/wav");
        await Assert.That(audioBytes.Length).IsGreaterThan(0);

        var assertions = await client.GetFromJsonAsync<JsonElement>(
            "/__llm-tck/assertions",
            _jsonOptions
        );
        await Assert.That(assertions.GetProperty("matched").GetInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task StreamingChatCompletion_ReturnsServerSentChunksAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "streaming-blue-whale",
                scenario => scenario
                    .ForModel("llm-tck-chat")
                    .WhenUserContains("stream")
                    .Responds("blue whale", "blue ", "whale")
            ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new
            {
                model = "llm-tck-chat",
                stream = true,
                messages = new[] { new { role = "user", content = "stream response" } },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(body).Contains("data: ");
        await Assert.That(body).Contains("blue ");
        await Assert.That(body).Contains("whale");
        await Assert.That(body).Contains("[DONE]");

        var ids = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("data: {", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line["data: ".Length..]).RootElement.GetProperty("id").GetString())
            .Distinct()
            .ToList();
        await Assert.That(ids).Count().IsEqualTo(1);
    }

    [Test]
    public async Task BearerTokenRequirement_ReturnsOpenAiErrorShapeAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.RequireBearerToken("test-key"));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/embeddings",
            new { model = "llm-tck-embedding", input = "hello" },
            _jsonOptions
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        await Assert.That(payload.GetProperty("error").GetProperty("code").GetString())
            .IsEqualTo("invalid_api_key");
    }

    [Test]
    public async Task ControlEndpoints_RequireConfiguredBearerTokenAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.RequireBearerToken("test-key"));
        using var client = host.GetTestClient();

        var assertions = await client.GetAsync("/__llm-tck/assertions");
        var reset = await client.PostAsync("/__llm-tck/reset", content: null);
        var configure = await client.PostAsJsonAsync(
            "/__llm-tck/configure",
            new { models = Array.Empty<object>(), chatScenarios = Array.Empty<object>() },
            _jsonOptions
        );

        await Assert.That(assertions.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(reset.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(configure.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");
        var authorizedAssertions = await client.GetAsync("/__llm-tck/assertions");

        authorizedAssertions.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task ProviderEndpoints_RejectUnknownModelsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/embeddings",
            new { model = "missing-embedding-model", input = "hello" },
            _jsonOptions
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        await Assert.That(payload.GetProperty("error").GetProperty("code").GetString())
            .IsEqualTo("llm_tck_unknown_model");
    }

    [Test]
    public async Task MalformedChatCompletion_ReturnsOpenAiBadRequestAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var malformed = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent("{", Encoding.UTF8, "application/json")
        );
        var missingMessages = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new { model = "llm-tck-chat", messages = (object?)null },
            _jsonOptions
        );

        await Assert.That(malformed.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingMessages.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var payload = await malformed.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        await Assert.That(payload.GetProperty("error").GetProperty("code").GetString())
            .IsEqualTo("invalid_request");
    }

    [Test]
    public async Task ConfigureEndpoint_AcceptsReadableJsonEnumsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var configure = await client.PostAsJsonAsync(
            "/__llm-tck/configure",
            new
            {
                models = new[]
                {
                    new { id = "docs-chat", kind = "chat" },
                    new { id = "docs-embedding", kind = "embedding" },
                    new { id = "docs-image", kind = "image" },
                    new { id = "docs-audio", kind = "audio" },
                },
                chatScenarios = new[]
                {
                    new
                    {
                        id = "docs-blue-whale",
                        modelId = "docs-chat",
                        match = new
                        {
                            mode = "contains",
                            messages = new[] { new { role = "user", content = "largest animal" } },
                        },
                        responses = new[]
                        {
                            new
                            {
                                content = "blue whale",
                                streamChunks = new[] { "blue ", "whale" },
                            },
                        },
                    },
                },
                defaultEmbeddingVector = new[] { 0.125f, 0.25f },
            },
            _jsonOptions
        );

        configure.EnsureSuccessStatusCode();

        var chat = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new
            {
                model = "docs-chat",
                messages = new[] { new { role = "user", content = "What is the largest animal?" } },
            },
            _jsonOptions
        );

        chat.EnsureSuccessStatusCode();
        var payload = await chat.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        await Assert.That(
                payload
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()
            )
            .IsEqualTo("blue whale");
    }

    [Test]
    public async Task ControlClient_ConfiguresReadsAssertionsAndResetsRuntimeAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();
        var controlClient = new LlmTckClient(client);

        await controlClient.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .AddChatScenario(
                    "control-client-blue-whale",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale")
                )
                .Build()
        );
        var chat = await client.PostAsJsonAsync(
            "/v1/chat/completions",
            new
            {
                model = "llm-tck-chat",
                messages = new[] { new { role = "user", content = "largest animal" } },
            },
            _jsonOptions
        );

        chat.EnsureSuccessStatusCode();
        var beforeReset = await controlClient.GetAssertionsAsync();

        await controlClient.ResetAsync();

        var afterReset = await controlClient.GetAssertionsAsync();

        await Assert.That(beforeReset.Matched).IsEqualTo(1);
        await Assert.That(afterReset.TotalEvents).IsEqualTo(0);
    }

    [Test]
    public async Task ProviderEndpoints_ReturnBadRequestForMissingRequiredFieldsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var missingEmbeddingModel = await client.PostAsJsonAsync(
            "/v1/embeddings",
            new { model = "", input = "hello" },
            _jsonOptions
        );
        var missingEmbeddingInput = await client.PostAsJsonAsync(
            "/v1/embeddings",
            new { model = "llm-tck-embedding", input = (object?)null },
            _jsonOptions
        );
        var missingImageModel = await client.PostAsJsonAsync(
            "/v1/images/generations",
            new { model = "", prompt = "draw this" },
            _jsonOptions
        );
        var missingImagePrompt = await client.PostAsJsonAsync(
            "/v1/images/generations",
            new { model = "llm-tck-image", prompt = "" },
            _jsonOptions
        );
        var missingAudioModel = await client.PostAsJsonAsync(
            "/v1/audio/speech",
            new { model = "", input = "speak this" },
            _jsonOptions
        );
        var missingAudioInput = await client.PostAsJsonAsync(
            "/v1/audio/speech",
            new { model = "llm-tck-audio", input = "" },
            _jsonOptions
        );
        var controlModels = await client.GetAsync("/__llm-tck/models");

        await Assert.That(missingEmbeddingModel.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingEmbeddingInput.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingImageModel.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingImagePrompt.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingAudioModel.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(missingAudioInput.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        controlModels.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task AudioSpeech_ReturnsTruthfulWavFixtureAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/v1/audio/speech",
            new { model = "llm-tck-audio", input = "audio fixture" },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();

        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("audio/wav");
        await Assert.That(Encoding.ASCII.GetString(bytes, 0, 4)).IsEqualTo("RIFF");
        await Assert.That(Encoding.ASCII.GetString(bytes, 8, 4)).IsEqualTo("WAVE");
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using global::Aspire.Hosting.Testing;
using ManagedCode.LlmTck.Client;
using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Tests.AspireIntegration;

public sealed class AspireIntegrationTests
{
    [Test]
    [Timeout(120_000)]
    public async Task AddLlmTck_StartsAspireProjectAndSupportsMicrosoftExtensionsAiAsync(
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var builder =
            await DistributedApplicationTestingBuilder
                .CreateAsync<global::Projects.ManagedCode_LlmTck_AppHost>(timeout.Token);

        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(
            "openai-compatible",
            timeout.Token
        );

        using var httpClient = app.CreateHttpClient("openai-compatible", "http");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-key");

        var controlClient = new LlmTckClient(httpClient);
        using IChatClient chatClient = new LlmTckChatClient(httpClient);
        using IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
            new LlmTckEmbeddingGenerator(httpClient);
        using IImageGenerator imageGenerator = new LlmTckImageGenerator(httpClient);

        var root = await httpClient.GetFromJsonAsync<JsonElement>("/", timeout.Token);
        var models = await httpClient.GetFromJsonAsync<JsonElement>("/v1/models", timeout.Token);
        var chat = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What color is the largest animal?")],
            cancellationToken: timeout.Token
        );
        var streamChunks = new List<string>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                    [new ChatMessage(ChatRole.User, "What color is the largest animal?")],
                    cancellationToken: timeout.Token
                )
                .WithCancellation(timeout.Token)
        )
        {
            streamChunks.Add(update.Text);
        }

        var embeddings = await embeddingGenerator.GenerateAsync(
            ["first", "second"],
            cancellationToken: timeout.Token
        );
        var image = await imageGenerator.GenerateAsync(
            new ImageGenerationRequest { Prompt = "compatibility marker" },
            cancellationToken: timeout.Token
        );
        var audio = await httpClient.PostAsJsonAsync(
            "/v1/audio/speech",
            new { model = "llm-tck-audio", input = "audio fixture" },
            timeout.Token
        );
        audio.EnsureSuccessStatusCode();
        var assertions = await controlClient.GetAssertionsAsync(timeout.Token);

        await Assert.That(models.GetProperty("data").GetArrayLength()).IsGreaterThanOrEqualTo(4);
        await Assert.That(root.GetProperty("endpoint").GetString())
            .IsEqualTo("https://api.example.com/v1");
        await Assert.That(root.GetProperty("openAiCompatibility").GetString()).IsEqualTo("true");
        await Assert.That(chat.Text).IsEqualTo("blue whale");
        await Assert.That(string.Concat(streamChunks)).IsEqualTo("blue whale");
        await Assert.That(embeddings).Count().IsEqualTo(2);
        await Assert.That(embeddings[0].Vector.Length).IsGreaterThan(0);
        await Assert.That(image.Contents).Count().IsEqualTo(1);
        await Assert.That(await audio.Content.ReadAsByteArrayAsync(timeout.Token))
            .Count()
            .IsGreaterThan(0);
        await Assert.That(assertions.Matched).IsGreaterThanOrEqualTo(5);
    }
}

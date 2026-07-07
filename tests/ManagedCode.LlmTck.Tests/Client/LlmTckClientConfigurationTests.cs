using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Tests.Client;

public sealed class LlmTckClientConfigurationTests
{
    [Test]
    public async Task ConfigureAsync_WithFluentClientApi_LoadsDatasetAndFixturesAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var httpClient = host.GetTestClient();
        var control = LlmTckClient.Create(httpClient, "test-key");

        await control.ConfigureAsync(
            tck => tck
                .RequireBearerToken("test-key")
                .UseChatModel("dataset-chat")
                .UseEmbeddingModel("dataset-embedding")
                .UseImageModel("dataset-image")
                .UseAudioModel("dataset-audio")
                .UseEmbeddingVector(0.9f, 0.8f)
                .UseImageDataUri("data:image/png;base64,Zm9v")
                .UseAudio([1, 2, 3, 4], "audio/test")
                .UseDataset(
                    "support-flow",
                    dataset => dataset
                        .AddChatScenario(
                            "dataset-blue",
                            scenario => scenario
                                .ForModel("dataset-chat")
                                .WhenUserContains("support")
                                .Responds("dataset answer", "dataset ", "answer")
                                .Responds("dataset answer", "dataset ", "answer")
                        )
                ),
            resetFirst: true
        );

        using var chatClient = control.CreateChatClient("dataset-chat");
        using var embeddingGenerator = control.CreateEmbeddingGenerator("dataset-embedding");
        using var imageGenerator = control.CreateImageGenerator("dataset-image");

        var chat = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "support request")]
        );
        var streamChunks = new List<string>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "support request")]
            )
        )
        {
            streamChunks.Add(update.Text);
        }

        var embeddings = await embeddingGenerator.GenerateAsync(["alpha"]);
        var image = await imageGenerator.GenerateAsync(
            new ImageGenerationRequest { Prompt = "fixture prompt" }
        );
        var audio = await control.GenerateAudioAsync("dataset-audio", "say this");
        var assertions = await control.GetAssertionsAsync();

        await Assert.That(chat.Text).IsEqualTo("dataset answer");
        await Assert.That(string.Concat(streamChunks)).IsEqualTo("dataset answer");
        await Assert.That(embeddings).Count().IsEqualTo(1);
        await Assert.That(embeddings[0].Vector.ToArray()).IsEquivalentTo([0.9f, 0.8f]);
        await Assert.That(image.Contents).Count().IsEqualTo(1);
        await Assert.That(audio.Bytes).IsEquivalentTo((byte[])[1, 2, 3, 4]);
        await Assert.That(audio.MediaType).IsEqualTo("audio/test");
        await Assert.That(assertions.Matched).IsGreaterThanOrEqualTo(5);
    }
}

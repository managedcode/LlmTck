using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.AI;

namespace ManagedCode.LlmTck.Tests.MicrosoftExtensionsAI;

public sealed class MicrosoftExtensionsAiClientTests
{
    [Test]
    public async Task TestChatClient_UsesDotnetExtensionsCallbackPatternAsync()
    {
        ChatMessage? capturedMessage = null;
        ChatOptions? capturedOptions = null;
        CancellationToken capturedToken = default;

        using IChatClient innerClient = new TestChatClient
        {
            GetResponseAsyncCallback = (messages, options, cancellationToken) =>
            {
                capturedMessage = messages.Single();
                capturedOptions = options;
                capturedToken = cancellationToken;
                return Task.FromResult(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, "blue whale"))
                );
            },
        };
        using var cts = new CancellationTokenSource();

        var response = await innerClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is the largest animal?")],
            new ChatOptions { ModelId = LlmTckKnownModelIds.Gpt41Mini },
            cts.Token
        );

        await Assert.That(capturedMessage?.Text).IsEqualTo("What is the largest animal?");
        await Assert.That(capturedOptions?.ModelId).IsEqualTo(LlmTckKnownModelIds.Gpt41Mini);
        await Assert.That(capturedToken.CanBeCanceled).IsTrue();
        await Assert.That(response.Text).IsEqualTo("blue whale");
    }

    [Test]
    public async Task TestChatClient_StreamsThroughCallbackPatternAsync()
    {
        ChatMessage? capturedMessage = null;
        ChatOptions? capturedOptions = null;
        CancellationToken capturedToken = default;

        using IChatClient innerClient = new TestChatClient
        {
            GetStreamingResponseAsyncCallback = (messages, options, cancellationToken) =>
            {
                capturedMessage = messages.Single();
                capturedOptions = options;
                capturedToken = cancellationToken;
                return GetUpdatesAsync();
            },
        };
        using var cts = new CancellationTokenSource();
        var updates = new List<string>();

        await foreach (
            var update in innerClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "stream")],
                new ChatOptions { ModelId = LlmTckKnownModelIds.Gpt41Mini },
                cts.Token
            )
        )
        {
            updates.Add(update.Text);
        }

        await Assert.That(capturedMessage?.Text).IsEqualTo("stream");
        await Assert.That(capturedOptions?.ModelId).IsEqualTo(LlmTckKnownModelIds.Gpt41Mini);
        await Assert.That(capturedToken.CanBeCanceled).IsTrue();
        await Assert.That(string.Concat(updates)).IsEqualTo("blue whale");

        static async IAsyncEnumerable<ChatResponseUpdate> GetUpdatesAsync()
        {
            await Task.Yield();
            yield return new(ChatRole.Assistant, "blue ");
            yield return new(ChatRole.Assistant, "whale");
        }
    }

    [Test]
    public async Task MicrosoftExtensionsAiClients_InvokeLlmTckThroughOpenAiCompatibilityEndpointsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .WithDefaultEmbeddingVector(0.125f, 0.25f, 0.5f)
                .AddChatScenario(
                    "extensions-ai-blue-whale",
                    scenario => scenario
                        .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                        .WhenUserContains("largest animal")
                        .Responds("blue whale", "blue ", "whale")
                        .Responds("blue whale", "blue ", "whale")
                ));
        using var httpClient = host.GetTestClient();
        using IChatClient chatClient = new LlmTckChatClient(httpClient);
        using IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
            new LlmTckEmbeddingGenerator(httpClient);
        using IImageGenerator imageGenerator = new LlmTckImageGenerator(httpClient);

        var chatResponse = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is the largest animal?")]
        );
        var streamUpdates = new List<string>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "What is the largest animal?")]
            )
        )
        {
            streamUpdates.Add(update.Text);
        }

        var embeddings = await embeddingGenerator.GenerateAsync(["alpha", "beta"]);
        var image = await imageGenerator.GenerateAsync(
            new ImageGenerationRequest { Prompt = "blue compatibility marker" }
        );

        await Assert.That(chatResponse.Text).IsEqualTo("blue whale");
        await Assert.That(string.Concat(streamUpdates)).IsEqualTo("blue whale");
        await Assert.That(embeddings).Count().IsEqualTo(2);
        await Assert.That(embeddings[0].Vector.Length).IsEqualTo(3);
        await Assert.That(image.Contents).Count().IsEqualTo(1);
        await Assert.That(image.Contents[0]).IsTypeOf<DataContent>();
    }
}

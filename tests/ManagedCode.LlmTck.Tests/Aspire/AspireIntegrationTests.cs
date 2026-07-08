using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Json;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;
using Azure.Core.Pipeline;
using global::Aspire.Hosting.Testing;
using ManagedCode.LlmTck.Aspire;
using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Control;
using Microsoft.Extensions.AI;
using ExtensionsChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ExtensionsChatRole = Microsoft.Extensions.AI.ChatRole;
using FoundryChatClient = Azure.AI.Inference.ChatCompletionsClient;
using FoundryEmbeddingClient = Azure.AI.Inference.EmbeddingsClient;
using OpenAiUserChatMessage = OpenAI.Chat.UserChatMessage;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Tests.AspireIntegration;

public sealed class AspireIntegrationTests
{
    private const string _resourceName = "llm-tck";
    private const string _apiKey = "test-key";
    private const string _chatModel = "llm-tck-chat";
    private const string _embeddingModel = "llm-tck-embedding";
    private const string _imageModel = "llm-tck-image";
    private const string _audioModel = "llm-tck-audio";

    [Test]
    public async Task AddLlmTckContainer_RemainsExplicitContainerOptInAsync()
    {
        var builder = DistributedApplicationTestingBuilder.Create([]);
        var llmTck = builder.AddLlmTckContainer().WithApiKey(_apiKey);

        await Assert.That(llmTck.Resource).IsTypeOf<LlmTckContainerResource>();
        await Assert.That(llmTck.GetHttpEndpoint().ToString()).IsNotEmpty();
    }

    [Test]
    [Timeout(360_000)]
    public async Task AddLlmTck_BuildsAppHostInTestAndSupportsConfiguredClientsAsync(
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var builder = DistributedApplicationTestingBuilder.Create([]);
        var llmTck = builder
            .AddLlmTck()
            .WithApiKey(_apiKey);
        var llmTckEndpointExpression = llmTck.GetHttpEndpoint().ToString();

        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(_resourceName, timeout.Token);

        using var httpClient = app.CreateHttpClient(_resourceName, "http");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var controlClient = new LlmTckClient(httpClient, _apiKey);
        await controlClient.ConfigureAsync(
            options => options
                .RequireBearerToken(_apiKey)
                .UseChatModel(_chatModel)
                .UseEmbeddingModel(_embeddingModel)
                .UseImageModel(_imageModel)
                .UseAudioModel(_audioModel)
                .UseEmbeddingVector(0.25f, 0.5f)
                .UseChatScenario(
                    "aspire-sdk-blue-whale",
                    scenario => scenario
                        .ForModel(_chatModel)
                        .WhenUserContains("aspire sdk")
                        .Responds("aspire blue whale", "aspire ", "blue ", "whale")
                        .Responds("aspire blue whale", "aspire ", "blue ", "whale")
                        .Responds("aspire blue whale", "aspire ", "blue ", "whale")
                        .Responds("aspire blue whale", "aspire ", "blue ", "whale")
                ),
            timeout.Token
        );

        using var chatClient = controlClient.CreateChatClient(_chatModel);
        using var embeddingGenerator = controlClient.CreateEmbeddingGenerator(_embeddingModel);
        using var imageGenerator = controlClient.CreateImageGenerator(_imageModel);

        var root = await httpClient.GetFromJsonAsync<JsonElement>("/", timeout.Token);
        var adminPage = await httpClient.GetStringAsync(LlmTckControlRoutes.Admin, timeout.Token);
        var models = await httpClient.GetFromJsonAsync<JsonElement>("/openai/v1/models", timeout.Token);
        var adminModels = await httpClient.GetFromJsonAsync<JsonElement>(
            LlmTckControlRoutes.Models,
            timeout.Token
        );
        var chat = await chatClient.GetResponseAsync(
            [new ExtensionsChatMessage(ExtensionsChatRole.User, "hello from aspire sdk")],
            cancellationToken: timeout.Token
        );
        var streamChunks = new List<string>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                    [new ExtensionsChatMessage(ExtensionsChatRole.User, "stream from aspire sdk")],
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
        var azureClient = new AzureOpenAIClient(
            ProviderEndpoint(httpClient, ProviderRoutes.AzureOpenAI, includeTrailingSlash: true),
            new ApiKeyCredential(_apiKey),
            new AzureOpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClient),
            }
        );
        var azureChatClient = azureClient.GetChatClient(_chatModel);
        var azureEmbeddingClient = azureClient.GetEmbeddingClient(_embeddingModel);
        var azureChat = await azureChatClient.CompleteChatAsync(
            [new OpenAiUserChatMessage("hello from azure aspire sdk")],
            cancellationToken: timeout.Token
        );
        var azureEmbedding = await azureEmbeddingClient.GenerateEmbeddingAsync(
            "azure embedding input",
            cancellationToken: timeout.Token
        );
        var azureEmbeddingValues = azureEmbedding.Value.ToFloats().ToArray();

        var foundryOptions = new AzureAIInferenceClientOptions
        {
            Transport = new HttpClientTransport(httpClient),
        };
        var foundryCredential = new AzureKeyCredential(_apiKey);
        var foundryEndpoint = ProviderEndpoint(
            httpClient,
            ProviderRoutes.MicrosoftFoundry,
            includeTrailingSlash: false
        );
        var foundryChatClient = new FoundryChatClient(
            foundryEndpoint,
            foundryCredential,
            foundryOptions
        );
        var foundryEmbeddingClient = new FoundryEmbeddingClient(
            foundryEndpoint,
            foundryCredential,
            foundryOptions
        );
        var foundryChat = await foundryChatClient.CompleteAsync(
            new ChatCompletionsOptions([new ChatRequestUserMessage("hello from foundry aspire sdk")])
            {
                Model = _chatModel,
            },
            timeout.Token
        );
        var foundryEmbeddings = await foundryEmbeddingClient.EmbedAsync(
            new EmbeddingsOptions(["first", "second"])
            {
                Model = _embeddingModel,
            },
            timeout.Token
        );
        var foundryEmbedding =
            foundryEmbeddings.Value.Data[0].Embedding.ToObjectFromJson<float[]>() ?? [];
        var image = await imageGenerator.GenerateAsync(
            new ImageGenerationRequest { Prompt = "compatibility marker" },
            cancellationToken: timeout.Token
        );
        var audio = await controlClient.GenerateAudioAsync(
            _audioModel,
            "audio fixture",
            timeout.Token
        );
        var assertions = await controlClient.GetAssertionsAsync(timeout.Token);
        var adminAssertions = await httpClient.GetFromJsonAsync<JsonElement>(
            LlmTckControlRoutes.Assertions,
            timeout.Token
        );

        await Assert.That(llmTckEndpointExpression).IsNotEmpty();
        await Assert.That(root.GetProperty("admin").GetString()).IsEqualTo(LlmTckControlRoutes.Admin);
        await Assert.That(adminPage).Contains("LLM&nbsp;TCK");
        await Assert.That(adminPage).Contains(LlmTckControlRoutes.Models);
        await Assert.That(adminPage).Contains(LlmTckControlRoutes.Assertions);
        await Assert.That(adminPage).Contains("Total tokens");
        await Assert.That(models.GetProperty("data").GetArrayLength()).IsGreaterThanOrEqualTo(4);
        await Assert.That(adminModels.GetArrayLength()).IsGreaterThanOrEqualTo(4);
        await Assert.That(chat.Text).IsEqualTo("aspire blue whale");
        await Assert.That(string.Concat(streamChunks)).IsEqualTo("aspire blue whale");
        await Assert.That(embeddings).Count().IsEqualTo(2);
        await Assert.That(embeddings[0].Vector.ToArray()).IsEquivalentTo(new[] { 0.25f, 0.5f });
        await Assert.That(azureChat.Value.Content[0].Text).IsEqualTo("aspire blue whale");
        await Assert.That(azureEmbeddingValues).IsEquivalentTo(new[] { 0.25f, 0.5f });
        await Assert.That(foundryChat.Value.Content).IsEqualTo("aspire blue whale");
        await Assert.That(foundryEmbeddings.Value.Data).Count().IsEqualTo(2);
        await Assert.That(foundryEmbedding).IsEquivalentTo(new[] { 0.25f, 0.5f });
        await Assert.That(image.Contents).Count().IsEqualTo(1);
        await Assert.That(audio.Bytes).Count().IsGreaterThan(0);
        await Assert.That(audio.MediaType).IsEqualTo("audio/wav");
        await Assert.That(assertions.Matched).IsGreaterThanOrEqualTo(8);
        await Assert.That(adminAssertions.GetProperty("matched").GetInt32()).IsGreaterThanOrEqualTo(8);
        await Assert.That(adminAssertions.GetProperty("totalTokens").GetInt32()).IsGreaterThan(0);
    }

    private static Uri ProviderEndpoint(
        HttpClient httpClient,
        string providerNamespace,
        bool includeTrailingSlash
    )
    {
        var path = providerNamespace.TrimStart('/');
        if (includeTrailingSlash)
        {
            path += "/";
        }

        return new Uri(httpClient.BaseAddress!, path);
    }
}

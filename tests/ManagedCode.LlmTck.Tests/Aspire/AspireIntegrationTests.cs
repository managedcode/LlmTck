using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using global::Aspire.Hosting.Testing;
using ManagedCode.LlmTck.Aspire;
using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Providers;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ExtensionsChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ExtensionsChatRole = Microsoft.Extensions.AI.ChatRole;
using FoundryChatClient = Azure.AI.Inference.ChatCompletionsClient;
using FoundryEmbeddingClient = Azure.AI.Inference.EmbeddingsClient;

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
    [Timeout(360_000)]
    public async Task AddLlmTck_BuildsAppHostInTestAndSupportsConfiguredClientsAsync(
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await EnsureLlmTckContainerImageAsync(timeout.Token);

        var builder = DistributedApplicationTestingBuilder.Create([]);
        builder
            .AddLlmTck()
            .WithImagePullPolicy(ImagePullPolicy.Never)
            .WithEndpoint("https://api.example.com/v1")
            .WithOpenAICompatibility()
            .WithAzureOpenAICompatibility()
            .WithFoundryCompatibility()
            .WithAnthropicCompatibility()
            .WithGeminiCompatibility()
            .WithGroqCompatibility()
            .WithMistralCompatibility()
            .WithOllamaCompatibility()
            .WithCohereCompatibility()
            .WithBedrockCompatibility()
            .WithOpenRouterCompatibility()
            .WithDeepSeekCompatibility()
            .WithPerplexityCompatibility()
            .WithApiKey(_apiKey);

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
        var models = await httpClient.GetFromJsonAsync<JsonElement>("/v1/models", timeout.Token);
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
            httpClient.BaseAddress!,
            new ApiKeyCredential(_apiKey),
            new AzureOpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClient),
            }
        );
        var azureChatClient = azureClient.GetChatClient(_chatModel);
        var azureEmbeddingClient = azureClient.GetEmbeddingClient(_embeddingModel);
        var azureChat = await azureChatClient.CompleteChatAsync(
            [new UserChatMessage("hello from azure aspire sdk")],
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
        var foundryChatClient = new FoundryChatClient(
            httpClient.BaseAddress!,
            foundryCredential,
            foundryOptions
        );
        var foundryEmbeddingClient = new FoundryEmbeddingClient(
            httpClient.BaseAddress!,
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

        await Assert.That(models.GetProperty("data").GetArrayLength()).IsGreaterThanOrEqualTo(4);
        await Assert.That(root.GetProperty("endpoint").GetString())
            .IsEqualTo("https://api.example.com/v1");
        var compatibility = root.GetProperty("compatibility");
        await Assert.That(compatibility.GetProperty("openAi").GetString()).IsEqualTo("true");
        await Assert.That(compatibility.GetProperty("azureOpenAi").GetString()).IsEqualTo("true");
        await Assert.That(compatibility.GetProperty("microsoftFoundry").GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Anthropic).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Gemini).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Groq).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Mistral).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Ollama).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Cohere).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Bedrock).GetString())
            .IsEqualTo("true");
        await Assert.That(compatibility.GetProperty("openRouter").GetString()).IsEqualTo("true");
        await Assert.That(compatibility.GetProperty("deepSeek").GetString()).IsEqualTo("true");
        await Assert.That(compatibility.GetProperty(LlmTckCompatibilityTags.Perplexity).GetString())
            .IsEqualTo("true");
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
    }

    private static async Task EnsureLlmTckContainerImageAsync(CancellationToken cancellationToken)
    {
        var imageReference = GetLlmTckImageReference();
        var image = await RunProcessAsync(
            "docker",
            ["image", "inspect", imageReference],
            workingDirectory: null,
            throwOnError: false,
            cancellationToken
        );
        if (image.ExitCode == 0)
        {
            return;
        }

        var repoRoot = FindRepositoryRoot();
        await RunProcessAsync(
            "docker",
            [
                "build",
                "-f",
                Path.Combine(
                    repoRoot,
                    "samples",
                    "ManagedCode.LlmTck.Service",
                    "Dockerfile"
                ),
                "-t",
                imageReference,
                repoRoot,
            ],
            repoRoot,
            throwOnError: true,
            cancellationToken
        );
    }

    private static string GetLlmTckImageReference()
    {
        return $"{LlmTckContainerImageTags.Registry}/{LlmTckContainerImageTags.Image}:{LlmTckContainerImageTags.Tag}";
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "ManagedCode.LlmTck.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the ManagedCode.LlmTck repository root.");
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        bool throwOnError,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start '{fileName}'.");
        }

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var result = new ProcessResult(
                process.ExitCode,
                await standardOutput,
                await standardError
            );

            if (throwOnError && result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{fileName} {string.Join(' ', arguments)} failed with exit code {result.ExitCode}."
                        + $"{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"
                );
            }

            return result;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError
    );
}

using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Configuration;

public sealed class LlmTckConfigurationBuilderTests
{
    [Test]
    public async Task CreateDefault_UsesOfficialOpenAiFixtureModelIdsAsync()
    {
        var configuration = LlmTckConfiguration.CreateDefault();

        await Assert.That(configuration.Models.Select(model => (model.Id, model.Kind)))
            .IsEquivalentTo(
                [
                    (LlmTckKnownModelIds.Gpt41Mini, LlmTckModelKind.Chat),
                    (LlmTckKnownModelIds.TextEmbedding3Small, LlmTckModelKind.Embedding),
                    (LlmTckKnownModelIds.GptImage1, LlmTckModelKind.Image),
                    (LlmTckKnownModelIds.Gpt4OMiniTts, LlmTckModelKind.Audio),
                    (LlmTckKnownModelIds.Sora2, LlmTckModelKind.Video),
                ]
            );
    }

    [Test]
    public async Task ConvenienceMethods_AddOfficialFixtureModelsAsync()
    {
        var configuration = new LlmTckConfigurationBuilder()
            .AddGpt41Mini()
            .AddTextEmbedding3Small()
            .AddGptImage1()
            .AddGpt4OMiniTts()
            .AddSora2()
            .AddReasoningChatModel("gpt-5-nano", 13)
            .Build();

        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.Gpt41Mini).Kind)
            .IsEqualTo(LlmTckModelKind.Chat);
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.TextEmbedding3Small).Kind)
            .IsEqualTo(LlmTckModelKind.Embedding);
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.GptImage1).Kind)
            .IsEqualTo(LlmTckModelKind.Image);
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.Gpt4OMiniTts).Kind)
            .IsEqualTo(LlmTckModelKind.Audio);
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.Sora2).Kind)
            .IsEqualTo(LlmTckModelKind.Video);
        await Assert.That(configuration.Models.Single(model => model.Id == "gpt-5-nano").ReasoningTokens)
            .IsEqualTo(13);
    }

    [Test]
    public async Task KnownOpenAiModelConvenienceMethods_AddSupportedAliasesAsync()
    {
        var configuration = new LlmTckConfigurationBuilder()
            .AddKnownOpenAiModels(reasoningTokens: 17)
            .Build();

        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Gpt55, LlmTckModelKind.Chat))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Gpt54Mini, LlmTckModelKind.Chat))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Gpt5Nano, LlmTckModelKind.Chat))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Gpt41, LlmTckModelKind.Chat))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Gpt4OMini, LlmTckModelKind.Chat))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.TextEmbedding3Large, LlmTckModelKind.Embedding))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.TextEmbeddingAda002, LlmTckModelKind.Embedding))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.GptImage2, LlmTckModelKind.Image))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.GptImage15, LlmTckModelKind.Image))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.GptImage1Mini, LlmTckModelKind.Image))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Tts1, LlmTckModelKind.Audio))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Tts1Hd, LlmTckModelKind.Audio))
            .IsTrue();
        await Assert.That(HasModel(configuration, LlmTckKnownModelIds.Sora2Pro, LlmTckModelKind.Video))
            .IsTrue();
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.Gpt55).ReasoningTokens)
            .IsEqualTo(17);
        await Assert.That(configuration.Models.Single(model => model.Id == LlmTckKnownModelIds.Gpt41).ReasoningTokens)
            .IsEqualTo(0);
    }

    [Test]
    public async Task Build_ReplacesDuplicateModelsAndScenariosAndSnapshotsNestedStateAsync()
    {
        var builder = new LlmTckConfigurationBuilder()
            .AddModel("docs-model", LlmTckModelKind.Chat)
            .AddModel("docs-model", LlmTckModelKind.Embedding)
            .WithDefaultEmbeddingVector(0.5f, 0.75f)
            .WithDefaultImageDataUri("data:image/png;base64,Zm9v")
            .WithDefaultAudio([1, 2, 3], "audio/test")
            .WithDefaultVideo([4, 5, 6], "video/test")
            .WithDefaultTranscriptionText("fixture transcript")
            .WithDefaultTranslationText("fixture translation")
            .SimulateRateLimitAfter(2)
            .SimulateContentFilter("blocked", " BLOCKED ", "forbidden")
            .AddDataset(
                "docs-dataset",
                dataset => dataset
                    .AddChatScenario(
                        "dataset-scenario",
                        scenario => scenario
                            .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                            .WhenUserContains("dataset")
                            .Responds("dataset response")
                    )
            )
            .AddChatScenario(
                "duplicate-scenario",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("old")
                    .Responds("old")
            )
            .AddChatScenario(
                "duplicate-scenario",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("new")
                    .Fails(418, "teapot", "Short and stout.")
                    .DelaysBy(25)
            );

        var configuration = builder.Build();

        configuration.Models.Clear();
        configuration.ChatScenarios[0].Match.Messages.Clear();
        configuration.Datasets[0].ChatScenarios[0].Match.Messages.Clear();
        configuration.ChatScenarios[0].Responses[0].StreamChunks.Add("mutated");
        configuration.DefaultEmbeddingVector.Clear();
        configuration.DefaultAudioBytes[0] = 99;
        configuration.DefaultVideoBytes[0] = 99;
        configuration.FaultSimulation.ContentFilterTerms.Clear();

        var rebuilt = builder.Build();

        await Assert.That(rebuilt.Models.Single(model => model.Id == "docs-model").Kind)
            .IsEqualTo(LlmTckModelKind.Embedding);
        await Assert.That(rebuilt.ChatScenarios).Count().IsEqualTo(1);
        await Assert.That(rebuilt.ChatScenarios[0].Match.Messages[0].Content).IsEqualTo("new");
        await Assert.That(rebuilt.ChatScenarios[0].Responses[0].Error?.Code).IsEqualTo("teapot");
        await Assert.That(rebuilt.ChatScenarios[0].Responses[0].DelayMilliseconds).IsEqualTo(25);
        await Assert.That(rebuilt.Datasets).Count().IsEqualTo(1);
        await Assert.That(rebuilt.Datasets[0].ChatScenarios[0].Match.Messages[0].Content)
            .IsEqualTo("dataset");
        await Assert.That(rebuilt.DefaultEmbeddingVector).IsEquivalentTo([0.5f, 0.75f]);
        await Assert.That(rebuilt.DefaultImageDataUri).IsEqualTo("data:image/png;base64,Zm9v");
        await Assert.That(rebuilt.DefaultAudioBytes).IsEquivalentTo((byte[])[1, 2, 3]);
        await Assert.That(rebuilt.DefaultAudioMediaType).IsEqualTo("audio/test");
        await Assert.That(rebuilt.DefaultVideoBytes).IsEquivalentTo((byte[])[4, 5, 6]);
        await Assert.That(rebuilt.DefaultVideoMediaType).IsEqualTo("video/test");
        await Assert.That(rebuilt.DefaultTranscriptionText).IsEqualTo("fixture transcript");
        await Assert.That(rebuilt.DefaultTranslationText).IsEqualTo("fixture translation");
        await Assert.That(rebuilt.FaultSimulation.MaxRequestsBeforeRateLimit).IsEqualTo(2);
        await Assert.That(rebuilt.FaultSimulation.ContentFilterTerms)
            .IsEquivalentTo(["blocked", "forbidden"]);
    }

    [Test]
    public async Task Builders_RejectInvalidInputsAsync()
    {
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().AddModel("", LlmTckModelKind.Chat)
        );
        await ShouldThrowAsync<ArgumentOutOfRangeException>(
            () => new LlmTckConfigurationBuilder().AddModel("gpt-5-nano", LlmTckModelKind.Chat, -1)
        );
        await ShouldThrowAsync<ArgumentOutOfRangeException>(
            () => new LlmTckConfigurationBuilder().AddReasoningChatModel("gpt-5-nano", 0)
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().RequireBearerToken(" ")
        );
        await ShouldThrowAsync<ArgumentNullException>(
            () => new LlmTckConfigurationBuilder().AddChatScenario("id", null!)
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultEmbeddingVector()
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultImageDataUri("https://example.com")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultAudio([], "audio/wav")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultAudio([1], "")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultVideo([], "video/mp4")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultVideo([1], "")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultTranscriptionText("")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().WithDefaultTranslationText("")
        );
        await ShouldThrowAsync<ArgumentOutOfRangeException>(
            () => new LlmTckConfigurationBuilder().SimulateRateLimitAfter(-1)
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().SimulateContentFilter()
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().SimulateContentFilter(" ")
        );

        await ShouldThrowAsync<ArgumentException>(() => new LlmTckScenarioBuilder("id").ForModel(""));
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckScenarioBuilder("id").RequireBearerToken("")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckScenarioBuilder("id").WhenUserContains("")
        );
        await ShouldThrowAsync<ArgumentNullException>(
            () => new LlmTckScenarioBuilder("id").WithExactMatch(null!)
        );
        await ShouldThrowAsync<ArgumentNullException>(
            () => new LlmTckScenarioBuilder("id").Responds(null!)
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckScenarioBuilder("id").Fails(400, "", "message")
        );
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckScenarioBuilder("id").Fails(400, "code", "")
        );
        await ShouldThrowAsync<ArgumentOutOfRangeException>(
            () => new LlmTckScenarioBuilder("id").DelaysBy(-1)
        );
        await ShouldThrowAsync<InvalidOperationException>(
            () => new LlmTckScenarioBuilder("id").Build()
        );
        await ShouldThrowAsync<ArgumentNullException>(
            () => new LlmTckScenarioDatasetBuilder("id").AddChatScenario("scenario", null!)
        );
    }

    [Test]
    public async Task DelaysBy_CreatesAResponseWhenNoResponseExistsYetAsync()
    {
        var scenario = new LlmTckScenarioBuilder("delayed-empty-response")
            .DelaysBy(10)
            .Build();

        await Assert.That(scenario.Responses).Count().IsEqualTo(1);
        await Assert.That(scenario.Responses[0].DelayMilliseconds).IsEqualTo(10);
    }

    private static async Task ShouldThrowAsync<TException>(Action action)
        where TException : Exception
    {
        Exception? exception = null;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception?.GetType()).IsEqualTo(typeof(TException));
    }

    private static bool HasModel(
        LlmTckConfiguration configuration,
        string id,
        LlmTckModelKind kind
    )
    {
        return configuration.Models.Any(model => model.Id == id && model.Kind == kind);
    }
}

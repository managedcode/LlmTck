using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Configuration;

public sealed class LlmTckConfigurationBuilderTests
{
    [Test]
    public async Task Build_ReplacesDuplicateModelsAndScenariosAndSnapshotsNestedStateAsync()
    {
        var builder = new LlmTckConfigurationBuilder()
            .AddModel("docs-model", LlmTckModelKind.Chat)
            .AddModel("docs-model", LlmTckModelKind.Embedding)
            .WithDefaultEmbeddingVector(0.5f, 0.75f)
            .WithDefaultImageDataUri("data:image/png;base64,Zm9v")
            .WithDefaultAudio([1, 2, 3], "audio/test")
            .AddDataset(
                "docs-dataset",
                dataset => dataset
                    .AddChatScenario(
                        "dataset-scenario",
                        scenario => scenario
                            .ForModel("llm-tck-chat")
                            .WhenUserContains("dataset")
                            .Responds("dataset response")
                    )
            )
            .AddChatScenario(
                "duplicate-scenario",
                scenario => scenario
                    .ForModel("llm-tck-chat")
                    .WhenUserContains("old")
                    .Responds("old")
            )
            .AddChatScenario(
                "duplicate-scenario",
                scenario => scenario
                    .ForModel("llm-tck-chat")
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
    }

    [Test]
    public async Task Builders_RejectInvalidInputsAsync()
    {
        await ShouldThrowAsync<ArgumentException>(
            () => new LlmTckConfigurationBuilder().AddModel("", LlmTckModelKind.Chat)
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
}

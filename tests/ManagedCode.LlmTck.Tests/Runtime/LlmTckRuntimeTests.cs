using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Runtime;

public sealed class LlmTckRuntimeTests
{
    private static readonly LlmTckChatRequest _largestAnimalRequest = new()
    {
        ModelId = "llm-tck-chat",
        Messages = [new LlmTckMessage { Role = "user", Content = "What is the largest animal?" }],
    };

    [Test]
    public async Task CompleteChatAsync_MatchesScenarioAndRecordsAssertionAsync()
    {
        const string userMessage = "What is the largest animal?";
        const string assistantMessage = "blue whale";
        var inputTokens = LlmTckTokenCounter.CountTextTokens(userMessage);
        var outputTokens = LlmTckTokenCounter.CountTextTokens(assistantMessage);

        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .AddChatScenario(
                    "blue-whale",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("largest animal")
                        .Responds(assistantMessage, "blue ", "whale")
                )
                .Build()
        );

        var result = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages =
                [
                    new LlmTckMessage
                    {
                        Role = "user",
                        Content = userMessage,
                    },
                ],
            }
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Content).IsEqualTo("blue whale");
        await Assert.That(result.StreamChunks).IsEquivalentTo(["blue ", "whale"]);

        var summary = runtime.GetAssertionSummary();
        await Assert.That(summary.Matched).IsEqualTo(1);
        await Assert.That(summary.Unmatched).IsEqualTo(0);
        await Assert.That(summary.InputTokens).IsEqualTo(inputTokens);
        await Assert.That(summary.OutputTokens).IsEqualTo(outputTokens);
        await Assert.That(summary.TotalTokens).IsEqualTo(inputTokens + outputTokens);
        await Assert.That(summary.Events[0].ScenarioId).IsEqualTo("blue-whale");
        await Assert.That(summary.Events[0].Usage?.InputTokens).IsEqualTo(inputTokens);
        await Assert.That(summary.Events[0].Usage?.OutputTokens).IsEqualTo(outputTokens);
        await Assert.That(summary.Events[0].Usage?.TotalTokens).IsEqualTo(
            inputTokens + outputTokens
        );
    }

    [Test]
    public async Task CompleteChatAsync_ReportsUnmatchedRequestAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(LlmTckConfiguration.CreateDefault());

        var result = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages =
                [
                    new LlmTckMessage
                    {
                        Role = "user",
                        Content = "No configured scenario matches this.",
                    },
                ],
            }
        );

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.StatusCode).IsEqualTo(404);
        await Assert.That(result.ErrorCode).IsEqualTo("llm_tck_unmatched_request");

        var summary = runtime.GetAssertionSummary();
        await Assert.That(summary.Unmatched).IsEqualTo(1);
    }

    [Test]
    public async Task CompleteChatAsync_ReportsAuthModelErrorsExhaustionAndScriptedErrorsAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .RequireBearerToken("test-key")
                .AddModel("other-chat", LlmTckModelKind.Chat)
                .AddChatScenario(
                    "other-model",
                    scenario => scenario
                        .ForModel("other-chat")
                        .WhenUserContains("largest animal")
                        .Responds("wrong model")
                )
                .AddChatScenario(
                    "tenant-only",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .RequireBearerToken("test-key")
                        .WhenUserContains("tenant")
                        .Responds("tenant response")
                )
                .AddChatScenario(
                    "single-use",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale")
                )
                .AddChatScenario(
                    "scripted-error",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("blocked fixture")
                        .Fails(400, "content_filter", "Scripted refusal.")
                )
                .Build()
        );

        var globalAuth = await runtime.CompleteChatAsync(_largestAnimalRequest, bearerToken: "wrong-key");
        var unknownModel = await runtime.CompleteChatAsync(
            _largestAnimalRequest with { ModelId = "missing-chat" },
            bearerToken: "test-key"
        );
        var tenantResponse = await runtime.CompleteChatAsync(
            _largestAnimalRequest with
            {
                Messages = [new LlmTckMessage { Role = "user", Content = "tenant request" }],
            },
            bearerToken: "test-key"
        );
        var first = await runtime.CompleteChatAsync(_largestAnimalRequest, bearerToken: "test-key");
        var exhausted = await runtime.CompleteChatAsync(_largestAnimalRequest, bearerToken: "test-key");
        var scriptedError = await runtime.CompleteChatAsync(
            _largestAnimalRequest with
            {
                Messages = [new LlmTckMessage { Role = "user", Content = "blocked fixture" }],
            },
            bearerToken: "test-key"
        );

        await Assert.That(globalAuth.StatusCode).IsEqualTo(401);
        await Assert.That(unknownModel.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(tenantResponse.Content).IsEqualTo("tenant response");
        await Assert.That(first.Content).IsEqualTo("blue whale");
        await Assert.That(exhausted.StatusCode).IsEqualTo(409);
        await Assert.That(exhausted.ErrorCode).IsEqualTo("llm_tck_scenario_exhausted");
        await Assert.That(scriptedError.StatusCode).IsEqualTo(400);
        await Assert.That(scriptedError.ErrorCode).IsEqualTo("content_filter");

        var summary = runtime.GetAssertionSummary();
        await Assert.That(summary.AuthFailed).IsEqualTo(1);
        await Assert.That(summary.ModelNotFound).IsEqualTo(1);
        await Assert.That(summary.Matched).IsEqualTo(2);
        await Assert.That(summary.ScenarioExhausted).IsEqualTo(1);
        await Assert.That(summary.ErrorsReturned).IsEqualTo(1);
    }

    [Test]
    public async Task CompleteChatAsync_UsesExactMatchAndRejectsShapeDriftAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .AddChatScenario(
                    "exact-contract",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WithExactMatch(
                            new LlmTckMessage { Role = "system", Content = "Return JSON only." },
                            new LlmTckMessage { Role = "user", Content = "Give me the invoice total." }
                        )
                        .Responds("""{"total":42.50}""")
                )
                .Build()
        );

        var roleDrift = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages =
                [
                    new LlmTckMessage { Role = "user", Content = "Return JSON only." },
                    new LlmTckMessage { Role = "user", Content = "Give me the invoice total." },
                ],
            }
        );
        var countDrift = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages = [new LlmTckMessage { Role = "system", Content = "Return JSON only." }],
            }
        );
        var exact = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages =
                [
                    new LlmTckMessage { Role = "system", Content = "Return JSON only." },
                    new LlmTckMessage { Role = "user", Content = "Give me the invoice total." },
                ],
            }
        );

        await Assert.That(roleDrift.ErrorCode).IsEqualTo("llm_tck_unmatched_request");
        await Assert.That(countDrift.ErrorCode).IsEqualTo("llm_tck_unmatched_request");
        await Assert.That(exact.Content).IsEqualTo("""{"total":42.50}""");
        await Assert.That(runtime.GetAssertionSummary().Unmatched).IsEqualTo(2);
    }

    [Test]
    public async Task Modalities_ReturnDeterministicDefaultsAndUseBearerTokenAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .RequireBearerToken("test-key")
                .AddModel("text-model", LlmTckModelKind.Chat)
                .WithDefaultEmbeddingVector(0.1f, 0.2f, 0.3f, 0.4f)
                .WithDefaultVideo([0, 0, 0, 24, 102, 116, 121, 112], "video/test")
                .WithDefaultTranscriptionText("hello from audio")
                .WithDefaultTranslationText("hello translated audio")
                .Build()
        );

        var unauthorized = await runtime.CreateEmbeddingAsync(
            "llm-tck-embedding",
            ["input"],
            bearerToken: "wrong-key"
        );
        var embeddings = await runtime.CreateEmbeddingAsync(
            "llm-tck-embedding",
            ["first", "second"],
            bearerToken: "test-key"
        );
        var image = await runtime.GenerateImageAsync(
            "llm-tck-image",
            "a compatibility test image",
            bearerToken: "test-key"
        );
        var audio = await runtime.GenerateAudioAsync(
            "llm-tck-audio",
            "hello",
            bearerToken: "test-key"
        );
        var video = await runtime.GenerateVideoAsync(
            "llm-tck-video",
            "generate a compatibility clip",
            bearerToken: "test-key"
        );
        var transcription = await runtime.TranscribeAudioAsync(
            "llm-tck-audio",
            "fixture.wav",
            bearerToken: "test-key"
        );
        var translation = await runtime.TranslateAudioAsync(
            "llm-tck-audio",
            "fixture.wav",
            bearerToken: "test-key"
        );

        await Assert.That(unauthorized.StatusCode).IsEqualTo(401);
        await Assert.That(embeddings.Vectors).Count().IsEqualTo(2);
        await Assert.That(embeddings.Vectors[0]).IsEquivalentTo([0.1f, 0.2f, 0.3f, 0.4f]);
        await Assert.That(image.DataUri).StartsWith("data:image/png;base64,");
        await Assert.That(audio.Bytes.Length).IsGreaterThan(0);
        await Assert.That(video.Bytes).IsEquivalentTo((byte[])[0, 0, 0, 24, 102, 116, 121, 112]);
        await Assert.That(video.MediaType).IsEqualTo("video/test");
        await Assert.That(transcription.Text).IsEqualTo("hello from audio");
        await Assert.That(translation.Text).IsEqualTo("hello translated audio");

        var summary = runtime.GetAssertionSummary();
        await Assert.That(summary.AuthFailed).IsEqualTo(1);
        await Assert.That(summary.Matched).IsEqualTo(6);
    }

    [Test]
    public async Task Modalities_RejectUnknownOrWrongKindModelsAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(LlmTckConfiguration.CreateDefault());

        var missingEmbedding = await runtime.CreateEmbeddingAsync("missing-embedding", ["input"]);
        var wrongKindImage = await runtime.GenerateImageAsync("llm-tck-chat", "prompt");
        var missingAudio = await runtime.GenerateAudioAsync("missing-audio", "input");
        var wrongKindTranscription = await runtime.TranscribeAudioAsync("llm-tck-chat", "fixture.wav");
        var wrongKindTranslation = await runtime.TranslateAudioAsync("llm-tck-chat", "fixture.wav");
        var wrongKindVideo = await runtime.GenerateVideoAsync("llm-tck-chat", "prompt");

        await Assert.That(missingEmbedding.IsSuccess).IsFalse();
        await Assert.That(missingEmbedding.StatusCode).IsEqualTo(404);
        await Assert.That(missingEmbedding.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(wrongKindImage.IsSuccess).IsFalse();
        await Assert.That(wrongKindImage.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(missingAudio.IsSuccess).IsFalse();
        await Assert.That(missingAudio.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(wrongKindTranscription.IsSuccess).IsFalse();
        await Assert.That(wrongKindTranscription.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(wrongKindTranslation.IsSuccess).IsFalse();
        await Assert.That(wrongKindTranslation.ErrorCode).IsEqualTo("llm_tck_unknown_model");
        await Assert.That(wrongKindVideo.IsSuccess).IsFalse();
        await Assert.That(wrongKindVideo.ErrorCode).IsEqualTo("llm_tck_unknown_model");

        var summary = runtime.GetAssertionSummary();
        await Assert.That(summary.ModelNotFound).IsEqualTo(6);
        await Assert.That(summary.Matched).IsEqualTo(0);
    }

    [Test]
    public async Task Modalities_ReportAuthFailuresForEveryProviderKindAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .RequireBearerToken("test-key")
                .Build()
        );

        var image = await runtime.GenerateImageAsync("llm-tck-image", "prompt", bearerToken: "wrong-key");
        var audio = await runtime.GenerateAudioAsync("llm-tck-audio", "input", bearerToken: "wrong-key");
        var transcription = await runtime.TranscribeAudioAsync(
            "llm-tck-audio",
            "fixture.wav",
            bearerToken: "wrong-key"
        );
        var translation = await runtime.TranslateAudioAsync(
            "llm-tck-audio",
            "fixture.wav",
            bearerToken: "wrong-key"
        );
        var video = await runtime.GenerateVideoAsync(
            "llm-tck-video",
            "prompt",
            bearerToken: "wrong-key"
        );

        await Assert.That(image.StatusCode).IsEqualTo(401);
        await Assert.That(image.ErrorCode).IsEqualTo("invalid_api_key");
        await Assert.That(audio.StatusCode).IsEqualTo(401);
        await Assert.That(audio.ErrorCode).IsEqualTo("invalid_api_key");
        await Assert.That(transcription.StatusCode).IsEqualTo(401);
        await Assert.That(transcription.ErrorCode).IsEqualTo("invalid_api_key");
        await Assert.That(translation.StatusCode).IsEqualTo(401);
        await Assert.That(translation.ErrorCode).IsEqualTo("invalid_api_key");
        await Assert.That(video.StatusCode).IsEqualTo(401);
        await Assert.That(video.ErrorCode).IsEqualTo("invalid_api_key");
        await Assert.That(runtime.GetAssertionSummary().AuthFailed).IsEqualTo(5);
    }

    [Test]
    public async Task ConfigureAsync_SnapshotsMutableConfigurationAsync()
    {
        var runtime = new LlmTckRuntime();
        var configuration = new LlmTckConfigurationBuilder()
            .AddChatScenario(
                "snapshot-blue-whale",
                scenario => scenario
                    .ForModel("llm-tck-chat")
                    .WhenUserContains("largest animal")
                    .Responds("blue whale", "blue ", "whale")
            )
            .Build();

        await runtime.ConfigureAsync(configuration);

        configuration.Models.Clear();
        configuration.ChatScenarios[0].Match.Messages.Clear();
        configuration.ChatScenarios[0].Responses[0].StreamChunks[0] = "mutated";
        configuration.DefaultEmbeddingVector[0] = 99f;

        var chat = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages = [new LlmTckMessage { Role = "user", Content = "largest animal" }],
            }
        );
        var embeddings = await runtime.CreateEmbeddingAsync("llm-tck-embedding", ["input"]);

        await Assert.That(chat.IsSuccess).IsTrue();
        await Assert.That(chat.StreamChunks).IsEquivalentTo(["blue ", "whale"]);
        await Assert.That(embeddings.Vectors[0][0]).IsEqualTo(0.125f);
    }

    [Test]
    public async Task ResetAsync_ClearsEventsAndScenarioPositionsWithoutReplacingConfigurationAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .AddChatScenario(
                    "resettable",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale")
                )
                .Build()
        );

        var beforeReset = await runtime.CompleteChatAsync(_largestAnimalRequest);

        await runtime.ResetAsync();

        var afterReset = await runtime.CompleteChatAsync(_largestAnimalRequest);

        await Assert.That(beforeReset.Content).IsEqualTo("blue whale");
        await Assert.That(afterReset.Content).IsEqualTo("blue whale");
        await Assert.That(runtime.GetAssertionSummary().Matched).IsEqualTo(1);
    }

    [Test]
    public async Task CancelledDelayedChat_DoesNotConsumeScenarioResponseAsync()
    {
        var runtime = new LlmTckRuntime();
        await runtime.ConfigureAsync(
            new LlmTckConfigurationBuilder()
                .AddChatScenario(
                    "delayed-blue-whale",
                    scenario => scenario
                        .ForModel("llm-tck-chat")
                        .WhenUserContains("largest animal")
                        .Responds("blue whale")
                        .DelaysBy(500)
                )
                .Build()
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        try
        {
            await runtime.CompleteChatAsync(
                new LlmTckChatRequest
                {
                    ModelId = "llm-tck-chat",
                    Messages = [new LlmTckMessage { Role = "user", Content = "largest animal" }],
                },
                cancellationToken: cts.Token
            );
        }
        catch (OperationCanceledException)
        {
        }

        var result = await runtime.CompleteChatAsync(
            new LlmTckChatRequest
            {
                ModelId = "llm-tck-chat",
                Messages = [new LlmTckMessage { Role = "user", Content = "largest animal" }],
            }
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Content).IsEqualTo("blue whale");
        await Assert.That(runtime.GetAssertionSummary().Matched).IsEqualTo(1);
    }
}

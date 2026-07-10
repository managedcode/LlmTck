using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.AzureOpenAI;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.DeepSeek;
using ManagedCode.LlmTck.Foundry;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Groq;
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Mistral;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.OpenRouter;
using ManagedCode.LlmTck.Perplexity;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Tests.Anthropic;
using ManagedCode.LlmTck.Tests.Azure;
using ManagedCode.LlmTck.Tests.Bedrock;
using ManagedCode.LlmTck.Tests.Cohere;
using ManagedCode.LlmTck.Tests.Gemini;
using ManagedCode.LlmTck.Tests.Ollama;
using ManagedCode.LlmTck.Tests.OpenAI;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class ProviderApiContractTests
{
    private const int _maxDocumentationReviewAgeDays = 180;
    private static readonly string[] _controlRoutePaths =
    [
        LlmTckControlRoutes.Admin,
        LlmTckControlRoutes.Models,
        LlmTckControlRoutes.Assertions,
        LlmTckControlRoutes.Configure,
        LlmTckControlRoutes.Reset,
    ];

    private static readonly Uri[] _officialDocumentationHosts =
    [
        new("https://developers.openai.com"),
        new("https://learn.microsoft.com"),
        new("https://docs.anthropic.com"),
        new("https://ai.google.dev"),
        new("https://console.groq.com"),
        new("https://docs.mistral.ai"),
        new("https://docs.ollama.com"),
        new("https://github.com/ollama/ollama"),
        new("https://docs.cohere.com"),
        new("https://docs.aws.amazon.com"),
        new("https://openrouter.ai"),
        new("https://api-docs.deepseek.com"),
        new("https://docs.perplexity.ai"),
    ];

    [Test]
    public async Task ProviderApiContracts_AreDocBackedAndCoverClaimedCapabilitiesAsync()
    {
        foreach (var profile in GetProfiles())
        {
            await Assert.That(IsOfficialDocumentationUrl(profile.ApiContract.DocumentationUrl)).IsTrue();
            var hasDocumentationDate = DateOnly.TryParse(
                profile.ApiContract.DocumentationRetrievedOn,
                out var documentationRetrievedOn
            );

            await Assert.That(hasDocumentationDate).IsTrue();
            await Assert.That(IsFreshDocumentationReview(documentationRetrievedOn)).IsTrue();
            await Assert.That(profile.ApiContract.DocumentationVersion).IsNotNull();
            await Assert.That(profile.ApiContract.DocumentationVersion).IsNotEmpty();
            await Assert.That(profile.ApiContract.Operations).IsNotEmpty();

            var documentedCapabilities = profile.ApiContract.Operations
                .SelectMany(operation => operation.Capabilities)
                .Distinct()
                .ToArray();
            var duplicateOperations = profile.ApiContract.Operations
                .GroupBy(operation => new { operation.Method, operation.Path })
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.Method} {group.Key.Path}")
                .ToArray();
            var unimplementedOperations = profile.ApiContract.Operations
                .Where(operation => !operation.ImplementedByHosting)
                .Select(operation => operation.Id)
                .ToArray();

            await Assert.That(profile.Capabilities.Except(documentedCapabilities)).IsEmpty();
            await Assert.That(duplicateOperations).IsEmpty();
            await Assert.That(unimplementedOperations).IsEmpty();
            await Assert.That(
                    profile.ApiContract.Operations.Any(operation =>
                        string.Equals(
                            operation.Path,
                            profile.DefaultEndpointPath,
                            StringComparison.Ordinal
                        )
                    ) || profile.ApiContract.Operations.Any(operation =>
                        operation.Path.StartsWith(profile.DefaultEndpointPath, StringComparison.Ordinal)
                    )
                )
                .IsTrue();

            foreach (var operation in profile.ApiContract.Operations)
            {
                await Assert.That(operation.Id).IsNotEmpty();
                await Assert.That(operation.Path).StartsWith("/");
                await Assert.That(IsSupportedHttpMethod(operation.Method)).IsTrue();
                await Assert.That(IsOfficialDocumentationUrl(operation.DocumentationUrl)).IsTrue();

                if (operation.SupportsStreaming)
                {
                    await Assert.That(
                            operation.Capabilities.Any(capability =>
                                capability
                                    is LlmTckProviderCapability.StreamingChat
                                        or LlmTckProviderCapability.StreamingImages
                                        or LlmTckProviderCapability.StreamingAudio
                            )
                        )
                        .IsTrue();
                }
            }
        }
    }

    [Test]
    public async Task HostingProviderRoutes_AreCoveredByImplementedDocumentedOperationsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        var providerNamespaces = GetProfiles()
            .Select(profile => GetExpectedProviderNamespace(profile.Id))
            .ToArray();
        var routes = host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<LlmTckProviderFallbackMetadata>() is null
            )
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    ?? ["GET"];

                return methods.Select(method => new ProviderRoute(
                    method,
                    endpoint.RoutePattern.RawText ?? string.Empty
                ));
            })
            .Where(route => !_controlRoutePaths.Contains(route.Path, StringComparer.Ordinal))
            .Where(route => IsProviderRoute(route.Path, providerNamespaces))
            .OrderBy(route => route.Method, StringComparer.Ordinal)
            .ThenBy(route => route.Path, StringComparer.Ordinal)
            .ToArray();

        var implementedOperations = GetProfiles()
            .SelectMany(profile => profile.ApiContract.Operations)
            .Where(operation => operation.ImplementedByHosting)
            .Select(operation => new ProviderRoute(operation.Method, operation.Path))
            .Distinct()
            .OrderBy(route => route.Method, StringComparer.Ordinal)
            .ThenBy(route => route.Path, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(routes).IsEquivalentTo(implementedOperations);
    }

    [Test]
    public async Task HostingProviderFallbackRoutes_AreExplicitlyMarkedDiagnosticsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        var providerNamespaces = GetProfiles()
            .Select(profile => GetExpectedProviderNamespace(profile.Id))
            .ToArray();
        var providerEndpoints = host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint =>
                IsProviderRoute(endpoint.RoutePattern.RawText ?? string.Empty, providerNamespaces)
            )
            .ToArray();
        var fallbackEndpoints = providerEndpoints
            .Where(endpoint => endpoint.RoutePattern.Parameters.Any(parameter => parameter.IsCatchAll))
            .ToArray();
        var markedEndpoints = providerEndpoints
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<LlmTckProviderFallbackMetadata>() is not null
            )
            .ToArray();

        await Assert.That(fallbackEndpoints.Length).IsEqualTo(providerNamespaces.Length);
        await Assert.That(markedEndpoints.Length).IsEqualTo(providerNamespaces.Length);
        foreach (var endpoint in fallbackEndpoints)
        {
            await Assert.That(endpoint.Metadata.GetMetadata<LlmTckProviderFallbackMetadata>())
                .IsNotNull();
        }

        foreach (var endpoint in markedEndpoints)
        {
            await Assert.That(
                    endpoint.RoutePattern.Parameters.Any(parameter => parameter.IsCatchAll)
                )
                .IsTrue();
        }
    }

    [Test]
    public async Task ProviderApiContracts_UseExplicitProviderNamespacesAsync()
    {
        foreach (var profile in GetProfiles())
        {
            var expectedNamespace = GetExpectedProviderNamespace(profile.Id);

            await Assert.That(profile.DefaultEndpointPath).StartsWith(expectedNamespace);

            foreach (var operation in profile.ApiContract.Operations)
            {
                await Assert.That(operation.Path).StartsWith(expectedNamespace);
            }
        }
    }

    [Test]
    public async Task ProviderBehaviorEvidence_CoversEveryImplementedOperationAsync()
    {
        var implementedOperations = GetImplementedOperations();
        var implementedOperationKeys = implementedOperations
            .Select(item => item.Key)
            .Distinct()
            .ToArray();
        var streamingOperationKeys = implementedOperations
            .Where(item => item.Operation.SupportsStreaming)
            .Select(item => item.Key)
            .Distinct()
            .ToArray();
        var evidence = GetBehaviorEvidence();
        var evidenceKeys = evidence
            .Select(item => new OperationKey(item.ProviderId, item.OperationId))
            .Distinct()
            .ToArray();
        var streamingEvidenceKeys = evidence
            .Where(item => item.CoversStreaming)
            .Select(item => new OperationKey(item.ProviderId, item.OperationId))
            .Distinct()
            .ToArray();
        var duplicateEvidence = evidence
            .GroupBy(item => new OperationKey(item.ProviderId, item.OperationId))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .ToArray();
        var missingEvidence = implementedOperationKeys
            .Except(evidenceKeys)
            .Select(key => key.ToString())
            .ToArray();
        var staleEvidence = evidenceKeys
            .Except(implementedOperationKeys)
            .Select(key => key.ToString())
            .ToArray();
        var missingStreamingEvidence = streamingOperationKeys
            .Except(streamingEvidenceKeys)
            .Select(key => key.ToString())
            .ToArray();
        var missingTestMethods = evidence
            .SelectMany(item => item.TestMethods.Select(method => new { item.TestType, Method = method }))
            .Where(item => item.TestType.GetMethod(item.Method) is null)
            .Select(item => $"{item.TestType.Name}.{item.Method}")
            .ToArray();
        var inventory = await File.ReadAllTextAsync(
            Path.Combine(
                FindRepositoryRoot(),
                "docs",
                "Testing",
                "AcceptanceCriteriaTestInventory.md"
            )
        );
        var undocumentedTestMethods = evidence
            .SelectMany(item => item.TestMethods)
            .Distinct()
            .Where(method => !inventory.Contains(method, StringComparison.Ordinal))
            .ToArray();

        await Assert.That(duplicateEvidence).IsEmpty();
        await Assert.That(missingEvidence).IsEmpty();
        await Assert.That(staleEvidence).IsEmpty();
        await Assert.That(missingStreamingEvidence).IsEmpty();
        await Assert.That(missingTestMethods).IsEmpty();
        await Assert.That(undocumentedTestMethods).IsEmpty();
    }

    private static bool IsFreshDocumentationReview(DateOnly retrievedOn)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var oldestAccepted = today.AddDays(-_maxDocumentationReviewAgeDays);
        return retrievedOn >= oldestAccepted && retrievedOn <= today;
    }

    private static bool IsOfficialDocumentationUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && _officialDocumentationHosts.Any(host =>
                string.Equals(uri.Host, host.Host, StringComparison.OrdinalIgnoreCase)
            );
    }

    private static bool IsProviderRoute(string path, IReadOnlyCollection<string> providerNamespaces)
    {
        return providerNamespaces.Any(providerNamespace =>
            path.StartsWith(providerNamespace, StringComparison.Ordinal)
        );
    }

    private static bool IsSupportedHttpMethod(string method)
    {
        return method is "DELETE" or "GET" or "HEAD" or "POST";
    }

    private static string GetExpectedProviderNamespace(string providerId)
    {
        return providerId switch
        {
            LlmTckCompatibilityTags.OpenAI => LlmTckProviderRouteNamespaces.OpenAI,
            LlmTckCompatibilityTags.AzureOpenAI => LlmTckProviderRouteNamespaces.AzureOpenAI,
            LlmTckCompatibilityTags.MicrosoftFoundry => LlmTckProviderRouteNamespaces.MicrosoftFoundry,
            LlmTckCompatibilityTags.Anthropic => LlmTckProviderRouteNamespaces.Anthropic,
            LlmTckCompatibilityTags.Gemini => LlmTckProviderRouteNamespaces.Gemini,
            LlmTckCompatibilityTags.Groq => LlmTckProviderRouteNamespaces.Groq,
            LlmTckCompatibilityTags.Mistral => LlmTckProviderRouteNamespaces.Mistral,
            LlmTckCompatibilityTags.Ollama => LlmTckProviderRouteNamespaces.Ollama,
            LlmTckCompatibilityTags.Cohere => LlmTckProviderRouteNamespaces.Cohere,
            LlmTckCompatibilityTags.Bedrock => LlmTckProviderRouteNamespaces.Bedrock,
            LlmTckCompatibilityTags.OpenRouter => LlmTckProviderRouteNamespaces.OpenRouter,
            LlmTckCompatibilityTags.DeepSeek => LlmTckProviderRouteNamespaces.DeepSeek,
            LlmTckCompatibilityTags.Perplexity => LlmTckProviderRouteNamespaces.Perplexity,
            _ => throw new InvalidOperationException($"Unknown provider '{providerId}'."),
        };
    }

    private static (OperationKey Key, LlmTckProviderOperation Operation)[] GetImplementedOperations()
    {
        return GetProfiles()
            .SelectMany(profile => profile
                .ApiContract
                .Operations
                .Where(operation => operation.ImplementedByHosting)
                .Select(operation => (
                    new OperationKey(profile.Id, operation.Id),
                    Operation: operation
                )))
            .ToArray();
    }

    private static ProviderOperationEvidence[] GetBehaviorEvidence()
    {
        return
        [
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ModelsList,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.OpenAiEndpoints_ExposeModelsAndAllModalitiesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ChatCompletionsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.StreamingChatCompletion_ReturnsServerSentChunksAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ResponsesCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_ReturnResponseShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_StreamResponseServerSentEventsAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.EmbeddingsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.OpenAiEndpoints_ExposeModelsAndAllModalitiesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ImagesCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.ImageRoutes_ReturnDocumentedEditVariationAndStreamingShapesAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ImagesEditsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.ImageRoutes_ReturnDocumentedEditVariationAndStreamingShapesAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.ImagesVariationsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.ImageRoutes_ReturnDocumentedEditVariationAndStreamingShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.AudioSpeechCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.AudioSpeech_ReturnsTruthfulWavFixtureAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.AudioTranscriptionsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.AudioTranscription_ReturnsJsonTextAndStreamingShapesAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.AudioTranslationsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.AudioTranslation_ReturnsJsonTextAndRejectsStreamingAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosList,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosRetrieve,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosDelete,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosContentRetrieve,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosEditsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosExtensionsCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosRemix,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosCharactersCreate,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenAI,
                LlmTckProviderOperationIds.OpenAI.VideosCharactersRetrieve,
                typeof(OpenAiEndpointTests),
                [nameof(OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.ChatCompletionsCreate,
                typeof(AzureSdkCompatibilityTests),
                [nameof(AzureSdkCompatibilityTests.AzureOpenAiClient_CanUseDeploymentChatAndEmbeddingsAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.EmbeddingsCreate,
                typeof(AzureSdkCompatibilityTests),
                [nameof(AzureSdkCompatibilityTests.AzureOpenAiClient_CanUseDeploymentChatAndEmbeddingsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.ImagesCreate,
                typeof(AzureSdkCompatibilityTests),
                [nameof(AzureSdkCompatibilityTests.AzureOpenAiDeploymentRoutes_UseApiKeyAndDeploymentModelForModalitiesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.AudioSpeechCreate,
                typeof(AzureSdkCompatibilityTests),
                [nameof(AzureSdkCompatibilityTests.AzureOpenAiDeploymentRoutes_UseApiKeyAndDeploymentModelForModalitiesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.AudioTranscriptionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiAudioTranscriptionRoute_UsesDeploymentModelAndApiKeyAsync),
                ]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.AudioTranslationsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiAudioTranslationRoute_UsesDeploymentModelAndApiKeyAsync),
                ]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsList,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsRetrieve,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationJobsDelete,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsRetrieve,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsThumbnailRetrieve,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentRetrieve,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.AzureOpenAI,
                LlmTckProviderOperationIds.AzureOpenAI.VideoGenerationsContentHead,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.MicrosoftFoundry,
                LlmTckProviderOperationIds.MicrosoftFoundry.ChatCompletionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.MicrosoftFoundry,
                LlmTckProviderOperationIds.MicrosoftFoundry.EmbeddingsCreate,
                typeof(AzureSdkCompatibilityTests),
                [nameof(AzureSdkCompatibilityTests.AzureAiInferenceClients_CanUseFoundryChatAndEmbeddingsAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.MicrosoftFoundry,
                LlmTckProviderOperationIds.MicrosoftFoundry.ModelsChatCompletionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.MicrosoftFoundry,
                LlmTckProviderOperationIds.MicrosoftFoundry.ModelsEmbeddingsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.FoundryEmbeddingRoutes_ReturnEmbeddingShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Anthropic,
                LlmTckProviderOperationIds.Anthropic.MessagesCreate,
                typeof(AnthropicEndpointTests),
                [
                    nameof(AnthropicEndpointTests.MessagesEndpoint_ReturnsAnthropicMessageShapeAsync),
                    nameof(AnthropicEndpointTests.MessagesEndpoint_StreamsAnthropicServerSentEventsAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.ModelsGenerateContent,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.GenerateContentEndpoint_ReturnsGeminiCandidateShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.ModelsStreamGenerateContent,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.StreamGenerateContentEndpoint_ReturnsGeminiServerSentEventsAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.ModelsEmbedContent,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.EmbedContentEndpoint_ReturnsGeminiEmbeddingShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.ModelsPredictLongRunningVideo,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.ModelsOperationsGetVideo,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Gemini,
                LlmTckProviderOperationIds.Gemini.FilesGetGeneratedVideo,
                typeof(GeminiEndpointTests),
                [nameof(GeminiEndpointTests.PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.ChatCompletionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.ResponsesCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_ReturnResponseShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_StreamResponseServerSentEventsAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.AudioSpeechCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.GroqAudioSpeechRoute_ReturnsAudioAndValidatesDocumentedRequestAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.AudioTranscriptionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.GroqAudioTranscriptionRoute_ReturnsGroqTranscriptionShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.AudioTranslationsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.GroqAudioTranslationRoute_ReturnsGroqTranslationShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Groq,
                LlmTckProviderOperationIds.Groq.ModelsList,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleModelRoutes_ReturnModelListShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Mistral,
                LlmTckProviderOperationIds.Mistral.ChatComplete,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Mistral,
                LlmTckProviderOperationIds.Mistral.EmbeddingsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.MistralEmbeddingRoute_ReturnsEmbeddingShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Ollama,
                LlmTckProviderOperationIds.Ollama.ChatCreate,
                typeof(OllamaEndpointTests),
                [
                    nameof(OllamaEndpointTests.ChatEndpoint_WithStreamFalse_ReturnsOllamaChatShapeAsync),
                    nameof(OllamaEndpointTests.ChatEndpoint_DefaultStreamsOllamaJsonLinesAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Ollama,
                LlmTckProviderOperationIds.Ollama.EmbeddingsCreate,
                typeof(OllamaEndpointTests),
                [nameof(OllamaEndpointTests.EmbedEndpoint_ReturnsOllamaEmbeddingShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Cohere,
                LlmTckProviderOperationIds.Cohere.ChatCreate,
                typeof(CohereEndpointTests),
                [
                    nameof(CohereEndpointTests.ChatEndpoint_ReturnsCohereChatShapeAsync),
                    nameof(CohereEndpointTests.ChatEndpoint_StreamsCohereServerSentEventsAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Cohere,
                LlmTckProviderOperationIds.Cohere.EmbedCreate,
                typeof(CohereEndpointTests),
                [
                    nameof(CohereEndpointTests.EmbedEndpoint_ReturnsCohereEmbeddingShapeForTextsAsync),
                    nameof(CohereEndpointTests.EmbedEndpoint_ReturnsCohereEmbeddingShapeForInputsAsync),
                ]
            ),
            Evidence(
                LlmTckCompatibilityTags.Bedrock,
                LlmTckProviderOperationIds.Bedrock.Converse,
                typeof(BedrockEndpointTests),
                [nameof(BedrockEndpointTests.ConverseEndpoint_ReturnsBedrockConverseShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Bedrock,
                LlmTckProviderOperationIds.Bedrock.ConverseStream,
                typeof(BedrockEndpointTests),
                [nameof(BedrockEndpointTests.ConverseStreamEndpoint_ReturnsBedrockEventStreamShapeAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.Bedrock,
                LlmTckProviderOperationIds.Bedrock.InvokeModel,
                typeof(BedrockEndpointTests),
                [
                    nameof(BedrockEndpointTests.InvokeEndpoint_ReturnsTitanTextShapeForChatModelAsync),
                    nameof(BedrockEndpointTests.InvokeEndpoint_ReturnsTitanEmbeddingShapeForEmbeddingModelAsync),
                    nameof(BedrockEndpointTests.InvokeEndpoint_ReturnsImageShapeForImageModelAsync),
                ]
            ),
            Evidence(
                LlmTckCompatibilityTags.Bedrock,
                LlmTckProviderOperationIds.Bedrock.InvokeModelWithResponseStream,
                typeof(BedrockEndpointTests),
                [nameof(BedrockEndpointTests.InvokeModelWithResponseStreamEndpoint_ReturnsChunkBytesAsync)],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenRouter,
                LlmTckProviderOperationIds.OpenRouter.ChatCompletionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenRouter,
                LlmTckProviderOperationIds.OpenRouter.ResponsesCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_ReturnResponseShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_StreamResponseServerSentEventsAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.OpenRouter,
                LlmTckProviderOperationIds.OpenRouter.ModelsList,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleModelRoutes_ReturnModelListShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.DeepSeek,
                LlmTckProviderOperationIds.DeepSeek.ChatCompletionsCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
            Evidence(
                LlmTckCompatibilityTags.DeepSeek,
                LlmTckProviderOperationIds.DeepSeek.ModelsList,
                typeof(OpenAiCompatibleProviderRouteTests),
                [nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleModelRoutes_ReturnModelListShapeAsync)]
            ),
            Evidence(
                LlmTckCompatibilityTags.Perplexity,
                LlmTckProviderOperationIds.Perplexity.SonarCreate,
                typeof(OpenAiCompatibleProviderRouteTests),
                [
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync),
                    nameof(OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync),
                ],
                coversStreaming: true
            ),
        ];
    }

    private static ProviderOperationEvidence Evidence(
        string providerId,
        string operationId,
        Type testType,
        IReadOnlyList<string> testMethods,
        bool coversStreaming = false
    )
    {
        return new ProviderOperationEvidence(
            providerId,
            operationId,
            testType,
            testMethods,
            coversStreaming
        );
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ManagedCode.LlmTck.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find ManagedCode.LlmTck.slnx.");
    }

    private static LlmTckProviderProfile[] GetProfiles()
    {
        return
        [
            OpenAiCompatibility.Profile,
            AzureOpenAiCompatibility.Profile,
            MicrosoftFoundryCompatibility.Profile,
            AnthropicCompatibility.Profile,
            GeminiCompatibility.Profile,
            GroqCompatibility.Profile,
            MistralCompatibility.Profile,
            OllamaCompatibility.Profile,
            CohereCompatibility.Profile,
            BedrockCompatibility.Profile,
            OpenRouterCompatibility.Profile,
            DeepSeekCompatibility.Profile,
            PerplexityCompatibility.Profile,
        ];
    }

    private sealed record OperationKey(string ProviderId, string OperationId)
    {
        public override string ToString()
        {
            return $"{ProviderId}:{OperationId}";
        }
    }

    private sealed record ProviderOperationEvidence(
        string ProviderId,
        string OperationId,
        Type TestType,
        IReadOnlyList<string> TestMethods,
        bool CoversStreaming
    );

    private sealed record ProviderRoute(string Method, string Path);
}

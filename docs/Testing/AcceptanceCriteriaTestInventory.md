# Acceptance Criteria Test Inventory

| Criterion | Coverage |
| --- | --- |
| Runtime matches scripted chat scenarios | `LlmTckRuntimeTests.CompleteChatAsync_MatchesScenarioAndRecordsAssertionAsync` |
| Runtime exposes deterministic token usage on chat and video operation results | `LlmTckRuntimeTests.CompleteChatAsync_MatchesScenarioAndRecordsAssertionAsync`, `LlmTckRuntimeTests.Modalities_ReturnDeterministicDefaultsAndUseBearerTokenAsync` |
| Runtime reports unmatched requests | `LlmTckRuntimeTests.CompleteChatAsync_ReportsUnmatchedRequestAsync` |
| Runtime supports embeddings, images, audio, and auth | `LlmTckRuntimeTests.Modalities_ReturnDeterministicDefaultsAndUseBearerTokenAsync` |
| Runtime rejects unknown or wrong-kind models | `LlmTckRuntimeTests.Modalities_RejectUnknownOrWrongKindModelsAsync` |
| Runtime fault simulation applies configured content-filter and rate-limit errors across modalities | `LlmTckRuntimeTests.FaultSimulation_ReturnsContentFilterAcrossRuntimeModalitiesAsync`, `LlmTckRuntimeTests.FaultSimulation_RateLimitCountsRequestsAcrossModalitiesAndResetAsync` |
| Runtime snapshots mutable configuration input | `LlmTckRuntimeTests.ConfigureAsync_SnapshotsMutableConfigurationAsync` |
| Cancelled delayed chat does not consume queued response | `LlmTckRuntimeTests.CancelledDelayedChat_DoesNotConsumeScenarioResponseAsync` |
| OpenAI-compatible endpoints expose models and modalities | `OpenAiEndpointTests.OpenAiEndpoints_ExposeModelsAndAllModalitiesAsync` |
| Streaming endpoint emits SSE chunks and done marker | `OpenAiEndpointTests.StreamingChatCompletion_ReturnsServerSentChunksAsync` |
| Bearer failures use OpenAI error envelope | `OpenAiEndpointTests.BearerTokenRequirement_ReturnsOpenAiErrorShapeAsync` |
| Control endpoints require bearer token when configured | `OpenAiEndpointTests.ControlEndpoints_RequireConfiguredBearerTokenAsync` |
| Provider endpoints reject unknown models | `OpenAiEndpointTests.ProviderEndpoints_RejectUnknownModelsAsync` |
| Malformed chat payloads return OpenAI bad request envelope | `OpenAiEndpointTests.MalformedChatCompletion_ReturnsOpenAiBadRequestAsync` |
| Audio speech fixture advertises a matching media type | `OpenAiEndpointTests.AudioSpeech_ReturnsTruthfulWavFixtureAsync` |
| `IChatClient` fake follows dotnet/extensions delegate pattern | `MicrosoftExtensionsAiClientTests.TestChatClient_UsesDotnetExtensionsCallbackPatternAsync` |
| Streaming fake follows delegate pattern | `MicrosoftExtensionsAiClientTests.TestChatClient_StreamsThroughCallbackPatternAsync` |
| `Microsoft.Extensions.AI` clients call the compatibility server | `MicrosoftExtensionsAiClientTests.MicrosoftExtensionsAiClients_InvokeLlmTckThroughOpenAiCompatibilityEndpointsAsync` |
| Official OpenAI SDK clients call the namespaced OpenAI API root for streaming chat | `OpenAiSdkCompatibilityTests.OpenAiChatClient_CanUseNamespacedChatStreamingAsync` |
| Official Azure OpenAI SDK clients call deployment chat and embedding routes | `AzureSdkCompatibilityTests.AzureOpenAiClient_CanUseDeploymentChatAndEmbeddingsAsync` |
| Azure OpenAI deployment routes use deployment names, API keys, and documented modality envelopes | `AzureSdkCompatibilityTests.AzureOpenAiDeploymentRoutes_UseApiKeyAndDeploymentModelForModalitiesAsync` |
| Official Azure AI Inference clients call Foundry chat and embedding routes | `AzureSdkCompatibilityTests.AzureAiInferenceClients_CanUseFoundryChatAndEmbeddingsAsync` |
| Universal client API configures datasets, models, auth, and modality fixtures before a test | `LlmTckClientConfigurationTests.ConfigureAsync_WithFluentClientApi_LoadsDatasetAndFixturesAsync` |
| Provider packages expose stable compatibility profiles | `ProviderPackageCatalogTests.ProviderPackages_ExposeExpectedCompatibilityProfilesAsync` |
| Provider profiles are backed by official docs and every hosted provider route maps to a documented implemented operation | `ProviderApiContractTests.ProviderApiContracts_AreDocBackedAndCoverClaimedCapabilitiesAsync`, `ProviderApiContractTests.HostingProviderRoutes_AreCoveredByImplementedDocumentedOperationsAsync` |
| Every implemented provider operation has behavior-test evidence, and every streaming operation has streaming evidence | `ProviderApiContractTests.ProviderBehaviorEvidence_CoversEveryImplementedOperationAsync` |
| Provider-neutral fault simulation returns native provider error envelopes for every provider family | `ProviderFaultSimulationTests.ProviderFamilies_ReturnConfiguredContentFilterErrorsAsync`, `ProviderFaultSimulationTests.ProviderFamilies_ReturnConfiguredRateLimitErrorsAsync` |
| Anthropic Messages API route follows the documented create-message and streaming surface | `AnthropicEndpointTests.MessagesEndpoint_ReturnsAnthropicMessageShapeAsync`, `AnthropicEndpointTests.MessagesEndpoint_StreamsAnthropicServerSentEventsAsync`, `AnthropicEndpointTests.MessagesEndpoint_RequiresAnthropicVersionHeaderAsync`, `AnthropicEndpointTests.MessagesEndpoint_AcceptsAnthropicApiKeyHeaderAsync` |
| OpenAI-compatible provider route aliases are documented and executable for non-streaming and streaming chat | `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync`, `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_StreamServerSentChunksAsync`, `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleModelRoutes_ReturnModelListShapeAsync`, `OpenAiCompatibleProviderRouteTests.MistralEmbeddingRoute_ReturnsEmbeddingShapeAsync`, `OpenAiCompatibleProviderRouteTests.FoundryEmbeddingRoutes_ReturnEmbeddingShapeAsync` |
| OpenAI-compatible Responses API routes are documented and executable for non-streaming and streaming responses | `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_ReturnResponseShapeAsync`, `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_StreamResponseServerSentEventsAsync` |
| OpenAI image generation, editing, variation, and image SSE routes are documented and executable | `OpenAiEndpointTests.ImageRoutes_ReturnDocumentedEditVariationAndStreamingShapesAsync` |
| OpenAI-compatible audio transcription routes are documented and executable for multipart input, JSON/text output, OpenAI streaming events, Groq metadata, and Azure deployment/API-key routing | `OpenAiEndpointTests.AudioTranscription_ReturnsJsonTextAndStreamingShapesAsync`, `OpenAiCompatibleProviderRouteTests.GroqAudioTranscriptionRoute_ReturnsGroqTranscriptionShapeAsync`, `OpenAiCompatibleProviderRouteTests.AzureOpenAiAudioTranscriptionRoute_UsesDeploymentModelAndApiKeyAsync` |
| OpenAI-compatible audio translation routes are documented and executable for multipart input, JSON/text or verbose JSON output, no unsupported streaming flag, Groq metadata, and Azure deployment/API-key routing | `OpenAiEndpointTests.AudioTranslation_ReturnsJsonTextAndRejectsStreamingAsync`, `OpenAiCompatibleProviderRouteTests.GroqAudioTranslationRoute_ReturnsGroqTranslationShapeAsync`, `OpenAiCompatibleProviderRouteTests.AzureOpenAiAudioTranslationRoute_UsesDeploymentModelAndApiKeyAsync` |
| Groq OpenAI-compatible audio speech route is documented and executable with required voice, deterministic audio bytes, and Groq-specific response-format validation | `OpenAiCompatibleProviderRouteTests.GroqAudioSpeechRoute_ReturnsAudioAndValidatesDocumentedRequestAsync` |
| OpenAI and Azure OpenAI video routes are documented and executable for lifecycle metadata, media content, Azure preview jobs, and header-only content retrieval | `OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync`, `OpenAiCompatibleProviderRouteTests.AzureOpenAiVideoRoutes_FollowPreviewJobAndContentShapesAsync` |
| Ollama and Cohere native routes follow documented chat, streaming, and embedding surfaces | `OllamaEndpointTests.ChatEndpoint_WithStreamFalse_ReturnsOllamaChatShapeAsync`, `OllamaEndpointTests.ChatEndpoint_DefaultStreamsOllamaJsonLinesAsync`, `OllamaEndpointTests.EmbedEndpoint_ReturnsOllamaEmbeddingShapeAsync`, `CohereEndpointTests.ChatEndpoint_ReturnsCohereChatShapeAsync`, `CohereEndpointTests.ChatEndpoint_StreamsCohereServerSentEventsAsync`, `CohereEndpointTests.EmbedEndpoint_ReturnsCohereEmbeddingShapeForTextsAsync`, `CohereEndpointTests.EmbedEndpoint_ReturnsCohereEmbeddingShapeForInputsAsync` |
| Provider response envelopes expose deterministic input/output usage where the provider surface documents usage | `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleChatRoutes_ReturnChatCompletionShapeAsync`, `OpenAiCompatibleProviderRouteTests.OpenAiCompatibleResponsesRoutes_ReturnResponseShapeAsync`, `AnthropicEndpointTests.MessagesEndpoint_ReturnsAnthropicMessageShapeAsync`, `AnthropicEndpointTests.MessagesEndpoint_StreamsAnthropicServerSentEventsAsync`, `GeminiEndpointTests.GenerateContentEndpoint_ReturnsGeminiCandidateShapeAsync`, `GeminiEndpointTests.PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync`, `CohereEndpointTests.ChatEndpoint_ReturnsCohereChatShapeAsync`, `OllamaEndpointTests.ChatEndpoint_WithStreamFalse_ReturnsOllamaChatShapeAsync`, `BedrockEndpointTests.ConverseEndpoint_ReturnsBedrockConverseShapeAsync`, `BedrockEndpointTests.ConverseStreamEndpoint_ReturnsBedrockEventStreamShapeAsync` |
| Gemini native routes follow documented generateContent, streamGenerateContent, embedContent, and long-running Veo video/file surfaces | `GeminiEndpointTests.GenerateContentEndpoint_ReturnsGeminiCandidateShapeAsync`, `GeminiEndpointTests.StreamGenerateContentEndpoint_ReturnsGeminiServerSentEventsAsync`, `GeminiEndpointTests.EmbedContentEndpoint_ReturnsGeminiEmbeddingShapeAsync`, `GeminiEndpointTests.PredictLongRunningVideoEndpoint_ReturnsGeminiOperationAndGeneratedFileAsync` |
| Bedrock native runtime routes follow documented Converse, ConverseStream, InvokeModel, and InvokeModelWithResponseStream envelopes | `BedrockEndpointTests.ConverseEndpoint_ReturnsBedrockConverseShapeAsync`, `BedrockEndpointTests.ConverseStreamEndpoint_ReturnsBedrockEventStreamShapeAsync`, `BedrockEndpointTests.InvokeEndpoint_ReturnsTitanTextShapeForChatModelAsync`, `BedrockEndpointTests.InvokeModelWithResponseStreamEndpoint_ReturnsChunkBytesAsync`, `BedrockEndpointTests.InvokeEndpoint_ReturnsTitanEmbeddingShapeForEmbeddingModelAsync`, `BedrockEndpointTests.InvokeEndpoint_ReturnsImageShapeForImageModelAsync` |
| Aspire AppHost is built in test code, adds the package-owned TCK with `builder.AddLlmTck()`, resolves the provider URL from the Aspire resource endpoint, configures API key auth, starts the packaged .NET service without Docker, and serves implemented modalities plus official Azure SDK clients | `AspireIntegrationTests.AddLlmTck_BuildsAppHostInTestAndSupportsConfiguredClientsAsync` |

## September 2026 provider API refresh

- `AzureV1SdkCompatibilityTests.V1ChatAndEmbeddings_UseOfficialSdkWithoutApiVersionAsync`: Azure OpenAI and Microsoft Foundry v1 chat, streaming and base64 embeddings through the official OpenAI SDK, without a dated API version.
- `AzureV1SdkCompatibilityTests.V1Responses_UseOfficialSdkAndStreamAsync`: OpenAI, Azure OpenAI and Foundry Responses parsed and streamed through OpenAI 2.13.0.
- `AzureV1SdkCompatibilityTests.V1Responses_AcceptsApiKeyAndRejectsMissingCredentialsAsync`: Azure v1 API-key authentication and unauthorized requests preserving the response queue.
- `ProviderApiDriftTests.OpenRouterResponses_RejectsStateBeforeConsumingScenarioAsync`: stateless Responses validation, including an empty non-null previous response ID.
- `ProviderApiDriftTests.OllamaChat_ReportsCacheReadsInFinalStreamingChunkAsync`: cold and cached requests, with cache accounting on the final NDJSON chunk.
- `ProviderApiDriftTests.AnthropicZeroMaxTokens_WarmsCacheWithoutConsumingResponseAsync`: cache-only requests in JSON and SSE modes, zero output, and preserved scripted response.
- `ProviderApiDriftTests.GeminiStreaming_PreservesResponseIdAndReasoningUsageAsync`: JSON/default/SSE transport, stable response ID, distinct thought tokens and final total accounting.
- `ProviderApiDriftTests.ResponsesStreaming_UsesDocumentedEventsAndStableItemIdentityAsync`: complete Responses text event lifecycle, monotonically increasing sequence numbers and one output item ID across OpenAI, Groq and OpenRouter.
- `BedrockEndpointTests.ConverseStreamEndpoint_ReturnsBedrockEventStreamShapeAsync` and `InvokeModelWithResponseStreamEndpoint_ReturnsChunkBytesAsync`: binary AWS event-stream decoded by the AWS SDK, including fragmented headers, payload and CRC checksums.

- `BedrockSdkCompatibilityTests.ConverseStream_OfficialSdkReadsTextAndUsageAsync`: the official AmazonBedrockRuntimeClient parses the hosted binary stream into typed text, usage and stop events.

`BedrockEndpointTests.StreamingEndpoints_RejectInvalidRequestsWithoutConsumingFixtureAsync` verifies malformed, null and incomplete JSON plus unauthorized calls on both binary streaming routes; a subsequent authenticated request still receives the configured response.

## Project review regressions

| Acceptance criterion | Evidence |
| --- | --- |
| Pre-cancelled/concurrently cancelled calls preserve unconsumed responses | `ReviewRuntimeRegressionTests` |
| Dataset-local scenario IDs stay independent | `ReviewRuntimeRegressionTests.SameLocalScenarioId_RemainsIndependentAcrossDatasetsAndResetAsync` |
| Errors before generation have zero output/reasoning tokens | `ReviewRuntimeRegressionTests.RejectedAndScriptedErrorRequests_DoNotGenerateReasoningOrOutputUsageAsync` |
| Prompt cache has deterministic capacity/eviction | `ReviewRuntimeRegressionTests.PromptCache_EvictsOldestPrefixAtConfiguredCapacityAsync` |
| Invalid configuration preserves the previous runtime and traces | `ReviewHttpRegressionTests.InvalidConfigure_PreservesConfigurationAndTracesAsync` |
| Azure SDK default supports deployment chat, streaming and embeddings without a pinned version | `AzureSdkCompatibilityTests.AzureOpenAiClient_CanUseDeploymentChatAndEmbeddingsAsync` |
| Documented GA and preview API versions are accepted; unknown date-shaped versions are rejected before queue consumption | `ReviewHttpRegressionTests.AzureLegacyVersion_AcceptsDocumentedStableAndPreviewVersionsAsync` |
| Nested null provider inputs and malformed Azure versions return 400 | `ReviewHttpRegressionTests` |
| Streaming and MEAI usage survive to callers | `ReviewHttpRegressionTests.StreamingUsage_IsOptInAndFollowsTerminalChoiceAsync`, `ChatClient_PreservesUsageAndTerminalUpdate_EmbeddingsPreserveUsageAsync` |
| Legacy function options fail explicitly; implemented tools/schema retain full behavior | `ReviewCapabilityRegressionTests.LegacyFunctions_ReturnExplicitErrorWithoutConsumingFixtureAsync`, `ToolFixtureEndpointTests`, `ToolFixtureClientTests` |
| Tool selection, argument/schema mismatch and malformed history preserve the queue | `ToolFixtureValidationTests` |
| Multiple calls, mixed text/tool blocks, snapshots and cache keys retain tool data | `ToolFixtureStateTests` |
| Foundry inference requires its documented API version | `ReviewHttpRegressionTests.FoundryInference_RequiresDocumentedVersionWithoutConsumingQueueAsync` |
| Stored OpenAI/Azure video lifecycle and content respect deletion/reset | `ReviewVideoRegressionTests` |
| Actual Aspire NuGet contains assets and interactive dashboard works | `scripts/package-consumer/PackageTests.cs`, executed by `scripts/verify-package.py` outside the repository |
| Interrupted NuGet publishing remains resumable | `scripts/test-release.py` with local fake CLIs |

## Follow-up review regressions (2026-09-08)

- `FollowUpRuntimeTests.SummaryMutations_CannotChangeRetainedMessagesOrChunksAsync`: mutation of returned snapshots cannot alter retained diagnostics.
- `FixtureMismatch_IsCountedAndJournaledWithoutConsumingResponseAsync`: mismatch status, usage, journal correlation and queue recovery.
- `ExactMatch_RejectsUnexpectedToolMetadataWhileContainsKeepsWildcardsAsync`: null/empty tool metadata is strict under Exact.
- `FollowUpEndpointTests.ParallelTools_RespectRequestLimitAndPreserveQueueAsync`: OpenAI Chat/Responses and Anthropic, JSON and streaming.
- `ChatModel_IsRequiredExceptForAzureDeploymentRoutesAsync`: required wire model and deployment URL exception.
- `GeminiSchema_ValidatesNullableNestedAlternativesAndStringLimitsAsync` and `FollowUpSchemaTests`: nullable, all six int64 limits, nested alternatives, parameters, JSON Schema separation and malformed-schema rejection.
- `VideoCapacity_RejectsOverflowAndReclaimsDeletedBytesAsync`, `VideoCapacity_ValidatesConfigurationAndHandlesOversizedOrFailedResultsAsync`, `VideoCapacity_ReturnsConflictThroughProviderEndpointAsync`: count/byte overflow, cross-provider budget, reset/configure/delete and HTTP failures.
- `ToolFixtureClientTests.ExtensionsAi_RespectsSingleToolOptionWithoutConsumingFixtureAsync`: MEAI parallel option, both response modes.
- `ProviderCapabilityEvidenceTests.ClaimedToolAndSchemaCases_ExecutePositiveAndNegativeHttpEvidenceAsync`: all 13 provider profiles, every claimed tool/schema operation, supported JSON/stream modes, accept/reject outcomes with queue recovery and journal checks.
- `EvidenceGuard_RequiresExecutableProviderAndEdgeCasesAsync`: executable test attributes, complete provider arguments, named Gemini cases and all six parallel-option cases are mandatory.

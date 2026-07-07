# Acceptance Criteria Test Inventory

| Criterion | Coverage |
| --- | --- |
| Runtime matches scripted chat scenarios | `LlmTckRuntimeTests.CompleteChatAsync_MatchesScenarioAndRecordsAssertionAsync` |
| Runtime reports unmatched requests | `LlmTckRuntimeTests.CompleteChatAsync_ReportsUnmatchedRequestAsync` |
| Runtime supports embeddings, images, audio, and auth | `LlmTckRuntimeTests.Modalities_ReturnDeterministicDefaultsAndUseBearerTokenAsync` |
| Runtime rejects unknown or wrong-kind models | `LlmTckRuntimeTests.Modalities_RejectUnknownOrWrongKindModelsAsync` |
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
| Aspire AppHost starts and serves all implemented modalities | `AspireIntegrationTests.AddLlmTck_StartsAspireProjectAndSupportsMicrosoftExtensionsAiAsync` |

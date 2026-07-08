# Provider API Contract Coverage

## Goal

LLM TCK provider packages must not claim provider compatibility from memory or from an OpenAI-compatible shortcut. Every claimed provider method, route, streaming mode, modality, and API-version/header requirement is represented in the package profile's `ApiContract`.

The current contract covers the generative API surface claimed by the provider profiles: chat/messages, streaming chat, embeddings, image generation or image inference, audio/speech, video generation where documented, model listing where claimed, tools, and structured output. Provider account administration, billing, files, fine-tuning, batches, and unrelated platform management APIs are outside the profile surface until a package explicitly claims them.

## Contract Shape

Each `LlmTckProviderProfile` has:

- `Capabilities`: the provider capabilities this package claims.
- `ApiContract.DocumentationUrl`: the official source used for the provider.
- `ApiContract.DocumentationRetrievedOn`: the date the docs were checked.
- `ApiContract.DocumentationVersion`: the API version, preview marker, or required version header when the provider publishes one.
- `ApiContract.Operations`: documented HTTP operations with method, path, docs URL, streaming support, required header or version, and `ImplementedByHosting`.

`ImplementedByHosting` is deliberately separate from provider capability. It means `ManagedCode.LlmTck.Hosting` currently maps that exact method/path. For this supertest surface, every operation in a provider profile must be implemented. Do not add future or exploratory provider methods to `ApiContract.Operations` until `MapLlmTck()` exposes the route and a behavior test proves the shape.

## Sync Rules

When provider docs change or a new route is added:

1. Update the provider package profile first.
2. Use official provider documentation only.
3. Record the exact route, method, version/header requirement, and docs URL on the operation.
4. Mark `ImplementedByHosting = true` only after the ASP.NET route exists.
5. Add or update behavior tests for the request and response shape before claiming runtime support.
6. Update the test inventory when the acceptance surface changes.
7. Refresh `DocumentationRetrievedOn`; contract tests fail when the docs review is older than 180 days.

## Guard Tests

`ProviderApiContractTests.ProviderApiContracts_AreDocBackedAndCoverClaimedCapabilitiesAsync` fails when a provider profile claims a capability without a documented operation, uses a non-official docs URL, omits a retrieval date or version, has docs older than 180 days, duplicates a method/path row, keeps an unimplemented operation in the profile, or marks streaming without a streaming capability.

`ProviderApiContractTests.HostingProviderRoutes_AreCoveredByImplementedDocumentedOperationsAsync` fails when `MapLlmTck()` exposes a public provider route that is not represented by an implemented documented operation, or when a documented implemented operation has no matching route.

`ProviderApiContractTests.ProviderApiContracts_UseExplicitProviderNamespacesAsync` fails when a provider profile exposes a hosted route outside its provider namespace, for example an OpenAI route outside `/openai`, an Anthropic route outside `/anthropic`, or an Azure OpenAI route outside `/azure-openai`.

`AnthropicEndpointTests` prove the first native non-OpenAI provider route: `/anthropic/v1/messages` accepts Anthropic text messages, requires `anthropic-version: 2023-06-01`, accepts the provider-owned `x-api-key` header, returns Anthropic message/error JSON with deterministic usage, and streams Anthropic-named SSE events with usage on `message_start` and cumulative output usage on `message_delta`.

`OpenAiCompatibleProviderRouteTests` prove the documented OpenAI-compatible provider routes currently mapped by hosting: OpenAI `/openai/v1/chat/completions` and `/openai/v1/models`, Groq `/groq/openai/v1/chat/completions` and `/groq/openai/v1/models`, OpenRouter `/openrouter/api/v1/chat/completions` and `/openrouter/api/v1/models`, DeepSeek `/deepseek/v1/chat/completions` and `/deepseek/models`, Mistral `/mistral/v1/chat/completions` and `/mistral/v1/embeddings`, and Perplexity `/perplexity/v1/sonar`.

The same test class proves the OpenAI-compatible Responses API routes: OpenAI `/openai/v1/responses`, Groq `/groq/openai/v1/responses`, and OpenRouter `/openrouter/api/v1/responses`, including string input, structured message input with text and image parts, response output text shape, deterministic input/output usage, and Responses-style SSE streaming events.

OpenAI image coverage now follows the current official image surface for this profile under the TCK provider namespace: `/openai/v1/images/generations`, `/openai/v1/images/edits`, and `/openai/v1/images/variations`. `OpenAiEndpointTests.ImageRoutes_ReturnDocumentedEditVariationAndStreamingShapesAsync` proves JSON generation streaming with `image_generation.partial_image` and `image_generation.completed`, multipart edit output and `image_edit.*` streaming, multipart variation input for the DALL-E 2 route, deterministic base64 fixture output, and documented option validation.

OpenAI audio transcription is covered through `/openai/v1/audio/transcriptions` with multipart form input, JSON output, plain-text output, and documented `transcript.text.delta` / `transcript.text.done` SSE events for `stream=true`. Groq `/groq/openai/v1/audio/transcriptions` is covered with multipart input and Groq's `x_groq.id` response metadata. Azure OpenAI `/azure-openai/openai/deployments/{deployment}/audio/transcriptions?api-version=2024-10-21` is covered with deployment-owned model resolution, `api-key` auth, multipart input, and verbose JSON output. Audio translation is covered through OpenAI `/openai/v1/audio/translations`, Groq `/groq/openai/v1/audio/translations`, and Azure OpenAI `/azure-openai/openai/deployments/{deployment}/audio/translations?api-version=2024-10-21` with multipart input, JSON/text or verbose JSON output as documented, Groq metadata, Azure deployment-owned model resolution, and no unsupported streaming flag. Groq speech is covered through `/groq/openai/v1/audio/speech` with the documented `voice` requirement, Groq-specific response formats, and deterministic audio bytes.

OpenAI video is covered through the documented `/openai/v1/videos` lifecycle: create, list, retrieve, delete, content download with `variant`, edit, extend, remix, create character, and get character. `OpenAiEndpointTests.VideoRoutes_ReturnDocumentedOpenAiShapesAndValidateEnumsAsync` proves multipart create, documented seconds/size validation, video/list/delete envelopes, deterministic MP4 bytes, thumbnail bytes, and character payloads. Azure OpenAI video is covered through the current v1 preview job/generation surface: `/azure-openai/openai/v1/video/generations/jobs`, job get/list/delete, generation get, thumbnail, video content, and `HEAD` content headers with `api-key` auth and `api-version=preview`. The stale `/openai/v1/videos` shape is not claimed for Azure OpenAI.

`OllamaEndpointTests` and `CohereEndpointTests` prove native non-OpenAI chat and embedding routes: Ollama `/ollama/api/chat` with prompt/eval counts and default JSON-line streaming plus `/ollama/api/embed`, and Cohere `/cohere/v2/chat` with deterministic token usage, optional SSE streaming, and `/cohere/v2/embed` with typed `float` embeddings.

`GeminiEndpointTests` prove Google Gemini native routes: `/gemini/v1beta/models/{model}:generateContent`, `/gemini/v1beta/models/{model}:streamGenerateContent`, `/gemini/v1beta/models/{model}:embedContent`, `/gemini/v1beta/models/{model}:predictLongRunning`, `/gemini/v1beta/models/{model}/operations/{operationId}`, and `/gemini/v1beta/files/{fileId}`. The tests cover the `contents[].parts[]` envelope, `usageMetadata`, SSE streaming via `alt=sse`, `x-goog-api-key`/`key` API-key wiring, `embedding.values` output, long-running Veo operation names, completed `generateVideoResponse.generatedSamples[].video` metadata with operation-result `usageMetadata`, generated file metadata, and deterministic MP4 download bytes.

`BedrockEndpointTests` prove Amazon Bedrock native runtime routes: `/bedrock/model/{modelId}/converse` returns the documented Converse `output.message`, `stopReason`, `usage`, and `metrics` envelope with input/output token counts; `/bedrock/model/{modelId}/converse-stream` returns the documented stream event names `messageStart`, `contentBlockStart`, `contentBlockDelta`, `contentBlockStop`, `messageStop`, and `metadata` with usage; `/bedrock/model/{modelId}/invoke` returns documented Titan Text, Titan Embeddings, and image-generation response bodies for chat, embedding, and image model kinds; and `/bedrock/model/{modelId}/invoke-with-response-stream` returns documented `chunk.bytes` stream payloads.

This makes API drift visible in the normal test suite instead of relying on release notes or manual README review.

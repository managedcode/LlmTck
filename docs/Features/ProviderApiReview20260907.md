# Provider API review — 2026-09-07

## Scope and method

Release `0.1.1` refreshes the 13 existing provider packages and their 73 hosted operations. The review follows the generative profile scope in [Provider API Contract Coverage](ProviderApiContractCoverage.md): claimed inference routes, response envelopes, streaming, modalities, authentication conventions, usage and version constraints. It does not claim complete implementation of every provider platform API, account management, agent service, batch job, fine-tuning service or every model-specific optional parameter. Deterministic configured fixtures remain the source of generated content.

The review retrieved the 55 distinct documentation URLs referenced by the profiles after adding the Azure v1 sources. 51 returned HTTP 200; four Microsoft operation URLs returned 404. Broken Microsoft links were replaced with the maintained official references, and Anthropic links now target the current Claude API reference. `DocumentationRetrievedOn` records this review date for every profile. Retrieval alone is not behavior evidence: the operation inventory is checked against mapped routes and named behavior tests in `ProviderApiContractTests`.

## Findings and changes

| Provider | Official sources reviewed | Result |
| --- | --- | --- |
| OpenAI | [API reference](https://developers.openai.com/api/reference/), [Responses events](https://developers.openai.com/api/reference/resources/responses/streaming-events) and each profile's model, chat, embedding, image, audio and video operation reference | Corrected Responses text delta/completion event names; emit full text lifecycle, sequence numbers and a stable output item ID. Verified ordinary and streaming Responses with OpenAI 2.13.0. Existing image/audio/video operation shapes retain their focused behavior coverage. |
| Azure OpenAI | [v1 lifecycle](https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle), [deployment reference](https://learn.microsoft.com/en-us/azure/foundry/openai/reference), [preview modalities](https://learn.microsoft.com/en-us/azure/foundry/openai/reference-preview-latest) | Added `/azure-openai/openai/v1/chat/completions`, `/responses` and `/embeddings`. v1 accepts API-key or bearer authentication without requiring a dated query version. Retained the deployment API and the separate preview video job/generation lifecycle. |
| Microsoft Foundry | [Model Inference](https://learn.microsoft.com/en-us/rest/api/microsoft-foundry/modelinference/), [OpenAI v1](https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle) | Added the corresponding three `/microsoft-foundry/openai/v1/...` operations. Retained legacy `/models/...` and standalone inference routes and official Azure.AI.Inference SDK tests. |
| Anthropic | [Create a Message](https://platform.claude.com/docs/en/api/messages/create) | Implemented documented `max_tokens=0` cache warming: zero generated/reasoning tokens, no content blocks or text deltas, `max_tokens` stop reason and no consumption of the configured response. Existing model, credential, version-header and scripted-fault checks still run. Normal generation then reads the populated cache. |
| Gemini | [Generate content](https://ai.google.dev/api/generate-content), [embeddings](https://ai.google.dev/api/embeddings), [Veo](https://ai.google.dev/gemini-api/docs/video), [files](https://ai.google.dev/api/files), [REST streaming cookbook](https://github.com/google-gemini/cookbook/blob/main/quickstarts/rest/Streaming_REST.ipynb) | Streaming now uses SSE for `alt=sse` and a JSON array for the default/JSON transport. Every chunk shares one response ID. Final usage separates visible candidate tokens from `thoughtsTokenCount` and includes both in the total. Native embedding and video/file contracts remain covered. |
| Groq | [API reference](https://console.groq.com/docs/api-reference), [models](https://console.groq.com/docs/models) | Shared Responses event corrections also apply to Groq. Rechecked chat, Responses, model listing, multipart transcription/translation and speech formats against the existing provider-specific tests. |
| Mistral | [Chat](https://docs.mistral.ai/api/endpoint/chat), [embeddings](https://docs.mistral.ai/api/endpoint/embeddings) | Existing namespaced chat/embedding routes retained; streaming and provider cache accounting remain covered by route tests. |
| Ollama | [Chat](https://docs.ollama.com/api/chat), [embed](https://docs.ollama.com/api/embed) | Added documented `prompt_eval_cached_count`, with deterministic cold/cache-read behavior and final NDJSON-chunk usage. Ollama cache accounting uses its own policy and reports no cache-write usage. |
| Cohere | [v2 chat](https://docs.cohere.com/reference/chat), [v2 embed](https://docs.cohere.com/reference/embed) | Rechecked native v2 envelopes, SSE and typed float embeddings; existing native routes retained. |
| Amazon Bedrock | [Converse](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_Converse.html), [ConverseStream](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_ConverseStream.html), [InvokeModel](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_InvokeModel.html), [InvokeModelWithResponseStream](https://docs.aws.amazon.com/bedrock/latest/APIReference/API_runtime_InvokeModelWithResponseStream.html) | Replaced newline JSON mislabeled as `application/vnd.amazon.eventstream` with official AWS SDK framing, event headers and CRCs. Both streaming routes are decoded through AWS Core; a real AmazonBedrockRuntimeClient verifies typed Converse stream text, stop reason and usage. |
| OpenRouter | [API overview](https://openrouter.ai/docs/api_reference/overview), [Responses](https://openrouter.ai/docs/api_reference/responses/overview), [models](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties) | Corrected shared Responses events and reject `store=true` or any non-null `previous_response_id` with 400 before consuming a fixture. `store=false` and null/omitted previous ID remain accepted. |
| DeepSeek | [Chat](https://api-docs.deepseek.com/api/create-chat-completion/), [models](https://api-docs.deepseek.com/api/list-models) | Existing native-prefix chat/model routes and hit/miss prompt-cache accounting retained and regression tested. |
| Perplexity | [Sonar](https://docs.perplexity.ai/api-reference/sonar-post) | The current `/v1/sonar` path was already implemented under `/perplexity`; no route rename was necessary. Existing Sonar chat/streaming and usage tests remain in the provider matrix. |

## NuGet versions

Every centrally managed dependency and both Aspire AppHost SDK pins were queried against NuGet. Stable dependencies use current stable releases. The preview-only Azure.AI.Inference stays at its latest available `1.0.0-beta.5`. The test-only Azure.AI.OpenAI dependency uses `2.9.0-beta.1`: stable `2.1.0` throws `MissingMethodException` against OpenAI `2.13.0` because its `SerializedAdditionalRawData` integration predates the current SDK. The [official Azure SDK changelog](https://github.com/Azure/azure-sdk-for-net/blob/Azure.AI.OpenAI_2.9.0-beta.1/sdk/openai/Azure.AI.OpenAI/CHANGELOG.md) documents the compatibility refresh, and both deployment-route and Aspire integration tests verify this pair. Shipping provider packages do not depend on this preview client SDK.

- Aspire.Hosting, Aspire.Hosting.Testing and Aspire.AppHost.Sdk: `13.5.0` → `13.5.3`.
- Azure.AI.OpenAI (compatibility tests only): `2.1.0` → `2.9.0-beta.1`.
- Azure.Core: `1.61.0` → `1.62.0`.
- TUnit: `1.65.31` → `1.66.27`.
- OpenAI: explicitly pinned the current `2.13.0` SDK in compatibility tests instead of relying on the older Azure SDK's transitive minimum.
- Added AWSSDK.Core `4.0.102.3` to the Bedrock adapter for official event-stream encoding and AWSSDK.BedrockRuntime `4.0.101.5` to tests for client-level evidence.
- Repository-local NuGet tools: Roslynator `0.12.0` → `1.0.0`, Coverlet `8.0.0` → `10.0.1`, ReportGenerator `5.5.3` → `5.5.11`.
- Other existing direct dependencies already matched the latest stable versions at review time.

## Verification contract

The [acceptance inventory](../Testing/AcceptanceCriteriaTestInventory.md#september-2026-provider-api-refresh) names the added regressions. Focused validation includes provider contract/evidence tests, all changed native/provider route tests and official OpenAI/Azure/AWS SDK consumers. Closeout also requires the full format/build/test/diff gate, including test-owned Aspire AppHost startup, and packing every shipping project.

The new regressions assert state preservation, negative request validation, exact content, event identity/order, protocol decoding and usage totals. They make no external provider inference calls and add no retries to hide failures. Documentation review is a dated source comparison; passing fixture tests is not a claim of live-provider conformance for unimplemented optional features.

## Final local verification

- Restore and repository-local tool restore passed. The final outdated-package scan reports no newer stable direct dependency; the preview-only Azure.AI.Inference entry has no stable result and was checked separately against its NuGet version index.
- Full solution format verification and Release build passed. The Aspire SDK emits two `ASPIRE010` warnings because the sample and test AppHosts use the NuGet-provided runtime instead of opting into the CLI bundle; actual AppHost startup and client calls passed.
- Full suite: **196 passed, 0 failed, 0 skipped**, including provider contract/evidence, OpenAI/Azure/AWS clients and test-owned Aspire AppHost startup.
- Coverlet 10.0.1: **90.07% total production line coverage**, passing the repository's 90% gate with the existing production include/exclude scope.
- All **17 shipping NuGet packages** packed as **0.1.1**; package nuspec versions were inspected.
- `git diff --check` passed. These are local validation results, with no CI run or package publication implied.

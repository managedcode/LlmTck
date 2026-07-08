# LLM TCK Architecture

## Purpose

LLM TCK provides deterministic provider emulation for integration tests. Test suites can host a local compatibility server, script exact responses and failures, call it through normal client abstractions, and inspect assertion summaries after execution.

## Package Boundaries

- `ManagedCode.LlmTck` owns provider-neutral runtime state: models, scenarios, match rules, deterministic modality fixtures, auth checks, and assertion events.
- `ManagedCode.LlmTck.OpenAI` owns OpenAI-compatible DTOs and mapping from runtime results to wire responses.
- `ManagedCode.LlmTck.AzureOpenAI`, `ManagedCode.LlmTck.Foundry`, `ManagedCode.LlmTck.Anthropic`, `ManagedCode.LlmTck.Gemini`, `ManagedCode.LlmTck.Groq`, `ManagedCode.LlmTck.Mistral`, `ManagedCode.LlmTck.Ollama`, `ManagedCode.LlmTck.Cohere`, `ManagedCode.LlmTck.Bedrock`, `ManagedCode.LlmTck.OpenRouter`, `ManagedCode.LlmTck.DeepSeek`, and `ManagedCode.LlmTck.Perplexity` own provider compatibility profiles and provider-specific wire contracts as they are implemented.
- `ManagedCode.LlmTck.Hosting` exposes the runtime through ASP.NET Core endpoints.
- `ManagedCode.LlmTck.Client` exposes a control client and `Microsoft.Extensions.AI` clients.
- `ManagedCode.LlmTck.Aspire` adds a package-owned Aspire resource with `builder.AddLlmTck()`.
- `samples/ManagedCode.LlmTck.Service` is the runnable HTTP provider emulator.
- `samples/ManagedCode.LlmTck.AppHost` shows the consumer AppHost shape for the Aspire package.

## Runtime Flow

1. Tests configure the runtime through `AddLlmTck(...)` at host startup, through `LlmTckClient.ConfigureAsync(config => ...)`, or through `POST /__llm-tck/configure`.
2. Provider requests arrive through `/v1/*` endpoints.
3. Hosting maps provider requests into provider-neutral runtime requests.
4. The runtime checks bearer-token requirements, configured model IDs, model modality kind, and scenario match rules.
5. The provider adapter maps success or failure to the expected wire shape.
6. Tests call `/__llm-tck/assertions` to inspect matched, unmatched, unknown-model, auth-failed, exhausted, and error-returned events.

Control endpoints use the same bearer-token requirement when one is configured, so tests can expose the service without leaving runtime reset or reconfiguration open to unauthenticated callers.

## Determinism

Chat scenarios are queued. Each matched request consumes the next response. Streaming responses use explicit chunks. Datasets are named groups of active scenarios so tests can load conversation sets before a run. Embeddings return configured vectors. Image, audio, and video endpoints return fixed fixture content.

## Client Configuration Surface

`ManagedCode.LlmTck.Client` owns the universal pre-test configuration API. A test can create one `LlmTckClient`, optionally bind a bearer token, call `ConfigureAsync(config => ...)`, and then create `Microsoft.Extensions.AI` chat, embedding, and image clients from the same control client. Audio and video fixtures are reachable through `LlmTckClient.GenerateAudioAsync(...)` and `LlmTckClient.GenerateVideoAsync(...)` until first-party `Microsoft.Extensions.AI` abstractions exist.

The client builder wraps the provider-neutral configuration builder; it does not create a separate runtime model. This keeps startup configuration, control endpoint configuration, and raw JSON configuration on the same contract.

## Compatibility Strategy

OpenAI compatibility is the first implemented provider endpoint surface. The hosted surface also maps Azure OpenAI deployment routes, Microsoft Foundry / Azure AI Inference chat and embedding routes, and the native Anthropic Messages route. The provider matrix is explicit: OpenAI, Azure OpenAI, Microsoft Foundry, Anthropic, Gemini, Groq, Mistral, Ollama, Cohere, Amazon Bedrock, OpenRouter, DeepSeek, and Perplexity each have a package-level compatibility profile. Each profile carries a doc-backed `ApiContract` so claimed routes, methods, streaming modes, modalities, and version requirements can be checked against official provider documentation. Provider-specific DTOs and mapping belong in the matching provider package; provider-neutral scenario behavior stays in the core runtime.

Aspire integration adds the TCK through `builder.AddLlmTck()` without requiring consumer AppHosts to reference a service project or generated `Projects.*` metadata type. The package-owned resource uses the versioned `ghcr.io/managedcode/llm-tck` container image. Consumer resources reference the `LlmTckResource`, wait for it, and read its `http` endpoint through `GetHttpEndpoint()` or Aspire's `GetEndpoint("http")`.

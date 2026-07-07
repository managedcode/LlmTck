# LLM TCK Architecture

## Purpose

LLM TCK provides deterministic provider emulation for integration tests. Test suites can host a local compatibility server, script exact responses and failures, call it through normal client abstractions, and inspect assertion summaries after execution.

## Package Boundaries

- `ManagedCode.LlmTck` owns provider-neutral runtime state: models, scenarios, match rules, deterministic modality fixtures, auth checks, and assertion events.
- `ManagedCode.LlmTck.OpenAI` owns OpenAI-compatible DTOs and mapping from runtime results to wire responses.
- `ManagedCode.LlmTck.Hosting` exposes the runtime through ASP.NET Core endpoints.
- `ManagedCode.LlmTck.Client` exposes a control client and `Microsoft.Extensions.AI` clients.
- `ManagedCode.LlmTck.Aspire` adds AppHost convenience methods over Aspire project resources.
- `samples/ManagedCode.LlmTck.Service` is the runnable HTTP provider emulator.
- `samples/ManagedCode.LlmTck.AppHost` wires the service into Aspire.

## Runtime Flow

1. Tests configure the runtime through `AddLlmTck(...)` at host startup or through `POST /__llm-tck/configure`.
2. Provider requests arrive through `/v1/*` endpoints.
3. Hosting maps provider requests into provider-neutral runtime requests.
4. The runtime checks bearer-token requirements, configured model IDs, model modality kind, and scenario match rules.
5. The provider adapter maps success or failure to the expected wire shape.
6. Tests call `/__llm-tck/assertions` to inspect matched, unmatched, unknown-model, auth-failed, exhausted, and error-returned events.

Control endpoints use the same bearer-token requirement when one is configured, so tests can expose the service without leaving runtime reset or reconfiguration open to unauthenticated callers.

## Determinism

Chat scenarios are queued. Each matched request consumes the next response. Streaming responses use explicit chunks. Embeddings return configured vectors. Image and audio endpoints return fixed fixture content.

## Compatibility Strategy

OpenAI compatibility is the first implemented provider surface. Additional provider packages should follow the same shape: provider-specific DTOs and mapping in a separate package, no provider-specific behavior in the core runtime.

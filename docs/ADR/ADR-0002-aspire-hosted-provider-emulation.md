# ADR-0002: Aspire-Hosted Provider Emulation

## Status

Accepted

## Context

The library is intended for integration tests, not only unit tests. It must start as a real service under Aspire so application resources can reference an LLM-compatible endpoint during test runs.

## Decision

Ship an Aspire extension package that adds an LLM TCK project resource to an AppHost. The integration test builds the AppHost model directly in test code with `Aspire.Hosting.Testing`, adds the sample service through `ManagedCode.LlmTck.Aspire`, then calls the running service through `Microsoft.Extensions.AI`, Azure OpenAI, Microsoft Foundry, and HTTP endpoints.

## Consequences

The Aspire path is validated by a real start/wait/call flow. The package stays aligned with Aspire resource APIs instead of shelling out to CLI processes from tests.

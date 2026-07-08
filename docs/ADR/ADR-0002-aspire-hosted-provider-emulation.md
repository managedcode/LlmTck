# ADR-0002: Aspire-Hosted Provider Emulation

## Status

Accepted

## Context

The library is intended for integration tests, not only unit tests. It must start as a real service under Aspire so application resources can reference an LLM-compatible endpoint during test runs.

## Decision

Ship an Aspire extension package that adds a package-owned LLM TCK resource to an AppHost with `builder.AddLlmTck()`. The consumer AppHost should not need a service project reference, generated `Projects.*` metadata type, project path, Docker, or a container runtime. The default resource starts the packaged .NET LLM TCK service executable. Container hosting remains available through an explicit `builder.AddLlmTckContainer()` opt-in for deployment or container-runtime smoke tests. The integration test builds the AppHost model directly in test code with `Aspire.Hosting.Testing`, adds the TCK through `ManagedCode.LlmTck.Aspire`, starts the .NET resource, then calls the running service through `Microsoft.Extensions.AI`, Azure OpenAI, Microsoft Foundry, and HTTP endpoints.

## Consequences

The Aspire path is validated by a real start/wait/call flow. The package stays aligned with Aspire resource APIs and keeps the hosted service artifact behind the Aspire integration package surface. The default local AppHost path stays .NET-first, while Docker remains an explicit container mode rather than a hidden prerequisite.

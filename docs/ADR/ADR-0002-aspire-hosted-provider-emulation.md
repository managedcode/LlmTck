# ADR-0002: Aspire-Hosted Provider Emulation

## Status

Accepted

## Context

The library is intended for integration tests, not only unit tests. It must start as a real service under Aspire so application resources can reference an LLM-compatible endpoint during test runs.

## Decision

Ship an Aspire extension package that adds an LLM TCK project resource to an AppHost. The integration test references the sample AppHost and starts it with `Aspire.Hosting.Testing`, then calls the running service through `Microsoft.Extensions.AI` clients and HTTP endpoints.

## Consequences

The Aspire path is validated by a real start/wait/call flow. The package stays aligned with Aspire resource APIs instead of shelling out to CLI processes from tests.

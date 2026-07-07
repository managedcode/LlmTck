# LLM TCK Contributor Guide

## Scope

This repository builds `ManagedCode.LlmTck`, a deterministic Technology Compatibility Kit for LLM APIs. Treat the runtime, protocol adapters, clients, Aspire integration, docs, and samples as separate package surfaces.

Follows [MCAF](https://mcaf.managed-code.com/).

## Conversations (Self-Learning)

Learn the user's stable habits, preferences, and corrections. Record durable rules here instead of relying on chat history.

Before doing any non-trivial task, evaluate the latest user message.
If it contains a durable rule, correction, preference, or workflow change, update `AGENTS.md` first.
If it is only task-local scope, do not turn it into a lasting rule.

Update this file when the user gives:

- a repeated correction
- a permanent requirement
- a lasting preference
- a workflow change
- a high-signal frustration that indicates a rule was missed

Extract rules aggressively when the user says things equivalent to:

- "never", "don't", "stop", "avoid"
- "always", "must", "make sure", "should"
- "remember", "keep in mind", "note that"
- "from now on", "going forward"
- "the workflow is", "we do it like this"

Preferences belong in `## Preferences`:

- positive preferences go under `Likes`
- negative preferences go under `Dislikes`
- comparisons should become explicit rules or preferences

Corrections should update an existing rule when possible instead of creating duplicates.

Treat these as strong signals and record them immediately:

- anger, swearing, sarcasm, or explicit frustration
- ALL CAPS, repeated punctuation, or "don't do this again"
- the same mistake happening twice
- the user manually undoing or rejecting a recurring pattern

Do not record:

- one-off instructions for the current task
- temporary exceptions
- requirements that are already captured elsewhere without change

Rule format:

- one instruction per bullet
- place it in the right section
- capture the why, not only the literal wording
- remove obsolete rules when a better one replaces them

## Commands

- Restore: `dotnet restore ManagedCode.LlmTck.slnx`
- Build: `dotnet build ManagedCode.LlmTck.slnx --configuration Release --no-restore`
- Test: `dotnet test tests/ManagedCode.LlmTck.Tests/ManagedCode.LlmTck.Tests.csproj --configuration Release --no-build --verbosity normal`
- Pack: `for project in src/*/*.csproj; do dotnet pack "$project" --configuration Release --no-build --output artifacts/packages; done`
- Release: push a `vX.Y.Z` tag only after `Directory.Build.props` has the matching package version; the release workflow must create the GitHub Release and attach package artifacts, because a passing `main` CI run is not a release.
- Format check: `dotnet format ManagedCode.LlmTck.slnx --verify-no-changes`
- Tool restore: `dotnet tool restore`
- Coverage gate: production code line coverage must stay at or above 90%; measure with coverlet/reportgenerator when coverage expectations change.
- Coverage: `rm -rf tests/ManagedCode.LlmTck.Tests/TestResults/coverage-analysis && mkdir -p tests/ManagedCode.LlmTck.Tests/TestResults/coverage-analysis/raw && dotnet tool run coverlet tests/ManagedCode.LlmTck.Tests/bin/Release/net10.0/ManagedCode.LlmTck.Tests.dll --target "dotnet" --targetargs "test tests/ManagedCode.LlmTck.Tests/ManagedCode.LlmTck.Tests.csproj --configuration Release --no-build --verbosity normal" --format cobertura --format json --output tests/ManagedCode.LlmTck.Tests/TestResults/coverage-analysis/raw/coverage --include "[ManagedCode.LlmTck]*" --include "[ManagedCode.LlmTck.*]*" --exclude "[ManagedCode.LlmTck.Tests]*" --exclude "[ManagedCode.LlmTck.Service]*" --exclude "[ManagedCode.LlmTck.AppHost]*" --threshold 90 --threshold-type line --threshold-stat Total`
- Quality gate: `dotnet format ManagedCode.LlmTck.slnx --verify-no-changes --no-restore && dotnet build ManagedCode.LlmTck.slnx --configuration Release --no-restore && dotnet test tests/ManagedCode.LlmTck.Tests/ManagedCode.LlmTck.Tests.csproj --configuration Release --no-build --verbosity normal && git diff --check`

`global.json` opts `dotnet test` into `Microsoft.Testing.Platform`; do not add VSTest-specific logger arguments to normal test commands.

## Code Quality Skills

- Repository-local skills live in `.codex/skills`; keep them checked in with the repo.
- MCAF governance skills remain the `mcaf-*` set installed from the canonical MCAF tutorial.
- Code-quality skills are installed from the current Managed Code `dotnet-skills` catalog for `.NET Quality`, `Frontend Quality`, `Diagnostics & Metrics`, `Testing`, and `Testing Research`.
- Use `analyzer-config`, `code-analysis`, `format`, `quality-ci`, `roslynator`, `meziantou-analyzer`, `stylecop-analyzers`, `complexity`, and `resharper-clt` when changing analyzer, naming, formatting, or warning policy.
- Use `run-tests`, `tunit`, `coverage-analysis`, `coverlet`, `reportgenerator`, `stryker`, `assertion-quality`, `test-anti-patterns`, `test-gap-analysis`, and `test-smell-detection` when changing test behavior or coverage expectations.
- Use `biome`, `eslint`, `htmlhint`, `stylelint`, `webhint`, and `sonarjs` for browser-facing files under `site` or future frontend assets.
- Use `codeql`, `quickdup`, `cloc`, `analyzing-dotnet-performance`, `microbenchmarking`, and `profiling` for deeper static, duplication, size, or performance analysis.
- Root `.editorconfig` is the source of truth for formatting, naming, and C# style. Prefer changing it once over suppressing analyzer noise in individual files.

## Design Rules

- Prefer official .NET packages and abstractions before writing custom protocol code.
- Tests that fake `IChatClient` should use the dotnet/extensions pattern: a tiny fake class with delegate callbacks, not Moq or NSubstitute.
- Keep provider wire formats in provider packages such as `ManagedCode.LlmTck.OpenAI`; keep deterministic scenario behavior in `ManagedCode.LlmTck`.
- Keep the provider package matrix explicit. Do not ship the TCK as only OpenAI-compatible; maintain package surfaces for OpenAI, Azure OpenAI, Microsoft Foundry, Anthropic, Gemini, Groq, Mistral, Ollama, Cohere, Amazon Bedrock, OpenRouter, DeepSeek, and Perplexity.
- Azure OpenAI and Microsoft Foundry compatibility must be proven with official Azure SDK clients, not only raw HTTP requests.
- Compatibility tag values in C# code must come from named constants everywhere they are assigned or asserted, so `CompatibilityTags` cannot drift through inline string literals.
- The client package must expose a universal pre-test configuration API so tests can spawn a client, reset or configure the hosted TCK, load models, auth, datasets, scenarios, scripted errors, embeddings, images, and audio fixtures without hand-authoring raw DTOs.
- Aspire examples must show endpoint configuration with `.WithEndpoint(...)`, compatibility selection, and API key wiring so users see the complete integration path.
- Aspire integration must expose a package-owned `builder.AddLlmTck()` entry point so consumers can install the Aspire NuGet package and add the TCK without caller-supplied project paths or `Projects.*` metadata types.
- Aspire integration must start an Aspire AppHost in tests before a change is considered covered, with the AppHost model built directly in test code or a test fixture.
- Aspire test coverage should build the AppHost directly inside the test code or fixture; do not add a standalone `Obhost`/test AppHost file just to host Aspire for tests.
- Do not hide nondeterminism behind retries. Model responses, stream chunks, errors, auth requirements, embeddings, images, and audio fixtures should be explicit.

## Ownership Map

- `src/ManagedCode.LlmTck`: provider-neutral runtime, scenarios, assertions, and model catalog.
- `src/ManagedCode.LlmTck.OpenAI`: OpenAI-compatible request/response shapes and mapping.
- `src/ManagedCode.LlmTck.AzureOpenAI`: Azure OpenAI compatibility profile and future Azure OpenAI wire contracts.
- `src/ManagedCode.LlmTck.Foundry`: Microsoft Foundry compatibility profile and future Foundry wire contracts.
- `src/ManagedCode.LlmTck.Anthropic`: Anthropic Messages compatibility profile and future Anthropic wire contracts.
- `src/ManagedCode.LlmTck.Gemini`: Gemini compatibility profile and future Gemini wire contracts.
- `src/ManagedCode.LlmTck.Groq`: Groq compatibility profile and future Groq wire contracts.
- `src/ManagedCode.LlmTck.Mistral`: Mistral compatibility profile and future Mistral wire contracts.
- `src/ManagedCode.LlmTck.Ollama`: Ollama compatibility profile and future Ollama wire contracts.
- `src/ManagedCode.LlmTck.Cohere`: Cohere compatibility profile and future Cohere wire contracts.
- `src/ManagedCode.LlmTck.Bedrock`: Amazon Bedrock compatibility profile and future Bedrock wire contracts.
- `src/ManagedCode.LlmTck.OpenRouter`: OpenRouter compatibility profile and future OpenRouter wire contracts.
- `src/ManagedCode.LlmTck.DeepSeek`: DeepSeek compatibility profile and future DeepSeek wire contracts.
- `src/ManagedCode.LlmTck.Perplexity`: Perplexity compatibility profile and future Perplexity wire contracts.
- `src/ManagedCode.LlmTck.Hosting`: ASP.NET Core provider and control endpoints.
- `src/ManagedCode.LlmTck.Client`: control client and `Microsoft.Extensions.AI` clients.
- `src/ManagedCode.LlmTck.Aspire`: Aspire AppHost extension methods.
- `samples`: runnable AppHost and service sample.
- `tests`: TUnit unit, endpoint, client, and Aspire integration coverage.
- `docs`: architecture, feature specs, ADRs, and test inventory.
- `site`: static public landing page.

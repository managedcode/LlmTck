# ManagedCode.LlmTck.Aspire

This package owns Aspire AppHost extensions.

Extension methods should be declarative and should configure the package-owned `LlmTckResource` through Aspire primitives. Consumer examples must use `builder.AddLlmTck()` without service project paths or generated `Projects.*` metadata types. Every behavior change needs an Aspire integration test that starts the AppHost.

The resource's `http` endpoint is the provider endpoint for Aspire consumers. Do not add a separate provider-endpoint setter or hardcoded loopback URL; callers must use `GetEndpoint("http")` or `CreateHttpClient(...)` from the running Aspire resource.

Do not add `LLM_TCK_*_COMPATIBILITY` environment flags or fluent compatibility methods unless they drive real route/runtime behavior; Aspire compatibility proof belongs in provider-client tests, not config echo endpoints.

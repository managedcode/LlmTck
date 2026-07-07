# ManagedCode.LlmTck.Aspire

This package owns Aspire AppHost extensions.

Extension methods should be declarative and should configure the package-owned `LlmTckResource` through Aspire primitives. Consumer examples must use `builder.AddLlmTck()` without service project paths or generated `Projects.*` metadata types. Every behavior change needs an Aspire integration test that starts the AppHost.

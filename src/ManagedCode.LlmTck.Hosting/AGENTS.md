# ManagedCode.LlmTck.Hosting

This package maps `ILlmTckRuntime` to HTTP endpoints.

Keep endpoint behavior thin: parse request, read bearer token, call the runtime, map to provider response shape. Add endpoint tests for every new route or status-code behavior.

# Tests

Tests use TUnit and Microsoft.Testing.Platform.

Use manual fakes for `IChatClient` callback assertions. Keep OpenAI endpoint tests on `Microsoft.AspNetCore.TestHost`, and keep Aspire tests on `Aspire.Hosting.Testing` with the real sample AppHost.

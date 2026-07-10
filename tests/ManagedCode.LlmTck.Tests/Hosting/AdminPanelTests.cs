using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Scenarios;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ManagedCode.LlmTck.Tests.Hosting;

public sealed class AdminPanelTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task AdminPanel_ServesStyledHtmlShellAtRootAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.GetAsync(LlmTckControlRoutes.Admin);
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(LlmTckControlRoutes.Admin).IsEqualTo("/");
        await Assert.That(body).Contains("<!doctype html>");
        await Assert.That(body).Contains("""<meta name="generator" content="Blazor SSR">""");
        await Assert.That(body).Contains("""<body data-renderer="blazor-ssr">""");
        await Assert.That(body).Contains("LLM TCK request dashboard");
        await Assert.That(body).Contains(LlmTckControlRoutes.Models);
        await Assert.That(body).Contains(LlmTckControlRoutes.Assertions);
        await Assert.That(body).Contains("Fixtures / models");
        await Assert.That(body).Contains("Runtime actions");
        await Assert.That(body).Contains("Provider APIs");
        await Assert.That(body).Contains("Provider requests");
        await Assert.That(body).Contains("Runtime setup & diagnostics");
        await Assert.That(body).Contains("Request summary");
        await Assert.That(body).Contains("Cache read");
        await Assert.That(body).Contains("Cache write");
        await Assert.That(body).DoesNotContain("Calls / requests");
        await Assert.That(body).Contains("blazor.web.js");
        await Assert.That(body).DoesNotContain("setInterval(refresh, 4000)");
        await Assert.That(body).DoesNotContain("setTimeout(function ()");
        await Assert.That(body).DoesNotContain("autoCountdown");
        await Assert.That(body).Contains("Total tokens");
        await Assert.That(body).Contains("/openai");
        await Assert.That(body).Contains("/azure-openai");
        await Assert.That(body).Contains("/anthropic");
    }

    [Test]
    public async Task AdminPanel_StaysReachableWhenBearerTokenIsRequiredAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.RequireBearerToken("test-key"));
        using var client = host.GetTestClient();

        var page = await client.GetAsync(LlmTckControlRoutes.Admin);
        var protectedModels = await client.GetAsync(LlmTckControlRoutes.Models);

        // The shell renders without a token so operators can enter one; the data
        // endpoints it calls remain protected.
        page.EnsureSuccessStatusCode();
        await Assert.That(protectedModels.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AdminPanel_IsServedByBlazorServerSideRenderingAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var hostingProject = await File.ReadAllTextAsync(Path.Combine(
            repositoryRoot,
            "src/ManagedCode.LlmTck.Hosting/ManagedCode.LlmTck.Hosting.csproj"
        ));
        var endpointSource = await File.ReadAllTextAsync(Path.Combine(
            repositoryRoot,
            "src/ManagedCode.LlmTck.Hosting/LlmTckEndpointRouteBuilderExtensions.cs"
        ));
        var razorComponentPath = Path.Combine(
            repositoryRoot,
            "src/ManagedCode.LlmTck.Hosting/LlmTckAdminPage.razor"
        );
        var razorComponent = await File.ReadAllTextAsync(razorComponentPath);
        var panelComponentPath = Path.Combine(
            repositoryRoot,
            "src/ManagedCode.LlmTck.Hosting/LlmTckAdminPanel.razor"
        );
        var panelComponent = await File.ReadAllTextAsync(panelComponentPath);

        await Assert.That(File.Exists(razorComponentPath)).IsTrue();
        await Assert.That(File.Exists(panelComponentPath)).IsTrue();
        await Assert.That(hostingProject).Contains("Sdk=\"Microsoft.NET.Sdk.Razor\"");
        await Assert.That(hostingProject).Contains("Microsoft.FluentUI.AspNetCore.Components");
        await Assert.That(endpointSource).Contains("MapRazorComponents<LlmTckAdminPage>");
        await Assert.That(endpointSource).Contains("MapStaticAssets");
        await Assert.That(endpointSource).Contains("StaticWebAssetsLoader.UseStaticWebAssets");
        await Assert.That(endpointSource).Contains("catch (DirectoryNotFoundException exception)");
        await Assert.That(endpointSource).Contains("Skipping LLM TCK static web assets runtime manifest");
        await Assert.That(endpointSource).Contains("AddInteractiveServerComponents");
        await Assert.That(endpointSource).Contains("AddHttpClient");
        await Assert.That(endpointSource).Contains("AddFluentUIComponents");
        await Assert.That(endpointSource).Contains("AddInteractiveServerRenderMode");
        await Assert.That(endpointSource).Contains("AddLlmTckProviderHttpTracing");
        await Assert.That(endpointSource).Contains("DisableAntiforgery");
        await Assert.That(razorComponent).Contains("@page \"/\"");
        await Assert.That(razorComponent).Contains("InteractiveServerRenderMode");
        await Assert.That(razorComponent).Contains("LlmTckAdminPanel");
        await Assert.That(razorComponent).Contains("Prostir technical polish × Managed Code editorial warmth");
        await Assert.That(razorComponent).Contains("#a4f258");
        await Assert.That(razorComponent).Contains("#f9f7f5");
        await Assert.That(razorComponent).Contains("#db3276");
        await Assert.That(panelComponent).Contains("PeriodicTimer");
        await Assert.That(panelComponent).Contains("StateHasChanged");
        await Assert.That(panelComponent).Contains("ILlmTckProviderHttpTraceStore");
        await Assert.That(panelComponent).Contains("Provider requests");
        await Assert.That(panelComponent).Contains("Conversation");
        await Assert.That(panelComponent).Contains("Wire payloads");
        await Assert.That(panelComponent).Contains("Tokens & cache");
        await Assert.That(panelComponent).Contains("Latency");
        await Assert.That(panelComponent).Contains("data-testid=\"request-row\"");
        await Assert.That(panelComponent).Contains("<FluentDialog");
        await Assert.That(panelComponent).Contains("Id=\"runtime-dialog\"");
        await Assert.That(panelComponent).Contains("aria-controls=\"runtime-dialog\"");
        await Assert.That(panelComponent).Contains("aria-label=\"Runtime settings\"");
        await Assert.That(panelComponent).Contains("aria-label=\"Refresh runtime state\"");
        await Assert.That(panelComponent).Contains("aria-label=\"Live auto-refresh\"");
        await Assert.That(panelComponent).Contains("aria-expanded=\"@(_runtimeDialogHidden ? \"false\" : \"true\")\"");
        await Assert.That(panelComponent).Contains("@bind-Hidden=\"_runtimeDialogHidden\"");
        await Assert.That(panelComponent).Contains("@ondialogdismiss=\"CloseRuntimeDialog\"");
        await Assert.That(panelComponent).Contains("@ref=\"_runtimeDialogTrigger\"");
        await Assert.That(panelComponent).Contains("_runtimeDialogTrigger.FocusAsync()");
        await Assert.That(panelComponent).Contains("OpenRuntimeDialog");
        await Assert.That(panelComponent).Contains("CloseRuntimeDialog");
        await Assert.That(panelComponent).Contains("RuntimeResponseRole");
        await Assert.That(panelComponent).Contains("\"provider error\"");
        await Assert.That(panelComponent).Contains("Microsoft.AspNetCore.Components.Web");
        await Assert.That(panelComponent).DoesNotContain("fetch(");
        await Assert.That(endpointSource.Contains("Results.Content(LlmTckAdminPage.Html", StringComparison.Ordinal))
            .IsFalse();
    }

    [Test]
    public async Task AspirePackage_DoesNotShipBuildMachineStaticWebAssetsRuntimeManifestAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var aspireProject = await File.ReadAllTextAsync(Path.Combine(
            repositoryRoot,
            "src/ManagedCode.LlmTck.Aspire/ManagedCode.LlmTck.Aspire.csproj"
        ));

        await Assert.That(aspireProject).Contains("**\\*.staticwebassets.runtime.json");
    }

    [Test]
    public async Task ControlRoutes_UseRootAndAdminApiPathsAsync()
    {
        await Assert.That(LlmTckControlRoutes.Admin).IsEqualTo("/");
        await Assert.That(LlmTckControlRoutes.Models).IsEqualTo("/admin-api/models");
        await Assert.That(LlmTckControlRoutes.Assertions).IsEqualTo("/admin-api/assertions");
        await Assert.That(LlmTckControlRoutes.Configure).IsEqualTo("/admin-api/configure");
        await Assert.That(LlmTckControlRoutes.Reset).IsEqualTo("/admin-api/reset");
    }

    [Test]
    public async Task RuntimeEvents_CaptureIncomingRequestAndModelResponseAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "capture-scenario",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("color")
                    .Responds("blue whale")
            ));
        using var client = host.GetTestClient();

        var chat = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                messages = new[] { new { role = "user", content = "what color is the whale" } },
            },
            _jsonOptions
        );
        chat.EnsureSuccessStatusCode();

        var summary = await client.GetFromJsonAsync<JsonElement>(
            LlmTckControlRoutes.Assertions,
            _jsonOptions
        );
        var events = summary.GetProperty("events");
        var lastEvent = events[events.GetArrayLength() - 1];

        // Operators must be able to see exactly what was asked and what the model answered.
        await Assert.That(lastEvent.GetProperty("request").GetString())
            .Contains("user: what color is the whale");
        await Assert.That(lastEvent.GetProperty("response").GetString()).IsEqualTo("blue whale");
        await Assert.That(summary.GetProperty("inputTokens").GetInt32()).IsEqualTo(5);
        await Assert.That(summary.GetProperty("outputTokens").GetInt32()).IsEqualTo(2);
        await Assert.That(summary.GetProperty("reasoningTokens").GetInt32()).IsEqualTo(0);
        await Assert.That(summary.GetProperty("totalTokens").GetInt32()).IsEqualTo(7);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("inputTokens").GetInt32())
            .IsEqualTo(5);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("outputTokens").GetInt32())
            .IsEqualTo(2);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("reasoningTokens").GetInt32())
            .IsEqualTo(0);
        await Assert.That(lastEvent.GetProperty("usage").GetProperty("totalTokens").GetInt32())
            .IsEqualTo(7);
    }

    [Test]
    public async Task ProviderRequestTrace_CapturesProviderMultiTurnPayloadAndRuntimeUsageAsync()
    {
        var longSystemMessage = $"{new string('x', 900)} END_OF_LONG_PROMPT";
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "trace-multi-turn",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("color")
                    .Responds("blue whale")
            ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                messages = new[]
                {
                    new { role = "system", content = longSystemMessage },
                    new { role = "user", content = "What did you answer before?" },
                    new { role = "assistant", content = "A blue whale." },
                    new { role = "user", content = "What color is it now?" },
                },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var store = host.Services.GetRequiredService<ILlmTckProviderHttpTraceStore>();
        var trace = store.GetSnapshot().Single();
        var runtime = host.Services.GetRequiredService<ILlmTckRuntime>();
        var runtimeEvent = runtime.GetAssertionSummary().Events.Single();

        await Assert.That(trace.ProviderId).IsEqualTo(LlmTckCompatibilityTags.OpenAI);
        await Assert.That(trace.Method).IsEqualTo(HttpMethod.Post.Method);
        await Assert.That(trace.Path).IsEqualTo("/openai/v1/chat/completions");
        await Assert.That(trace.OperationId)
            .IsEqualTo(LlmTckProviderOperationIds.OpenAI.ChatCompletionsCreate);
        await Assert.That(trace.ModelId).IsEqualTo(LlmTckKnownModelIds.Gpt41Mini);
        await Assert.That(trace.StatusCode).IsEqualTo((int)HttpStatusCode.OK);
        await Assert.That(trace.DurationMilliseconds).IsNotNull();
        await Assert.That(trace.DurationMilliseconds!.Value).IsGreaterThanOrEqualTo(0d);
        await Assert.That(trace.RequestPreview).Contains("END_OF_LONG_PROMPT");
        await Assert.That(trace.ResponsePreview).Contains("blue whale");
        await Assert.That(trace.RuntimeResponse).IsEqualTo("blue whale");
        await Assert.That(trace.Messages.Count).IsEqualTo(4);
        await Assert.That(trace.Messages[0].Role).IsEqualTo("system");
        await Assert.That(trace.Messages[0].Content).IsEqualTo(longSystemMessage);
        await Assert.That(trace.Messages[1].Role).IsEqualTo("user");
        await Assert.That(trace.Messages[1].Content).IsEqualTo("What did you answer before?");
        await Assert.That(trace.Messages[2].Role).IsEqualTo("assistant");
        await Assert.That(trace.Messages[2].Content).IsEqualTo("A blue whale.");
        await Assert.That(trace.Messages[3].Role).IsEqualTo("user");
        await Assert.That(trace.Messages[3].Content).IsEqualTo("What color is it now?");
        await Assert.That(trace.Usage).IsNotNull();
        await Assert.That(trace.Usage!.TotalTokens).IsGreaterThan(0);
        await Assert.That(runtimeEvent.RequestId).IsEqualTo(trace.RequestId);
        await Assert.That(runtimeEvent.Messages.Count).IsEqualTo(4);
        await Assert.That(response.Headers.GetValues(LlmTckProviderHttpTraceOptions.DefaultRequestIdHeaderName).Single())
            .IsEqualTo(trace.RequestId);
    }

    [Test]
    public async Task ProviderRequestTrace_CorrelatesMultipartAudioModelAndOperationAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options
                .WithDefaultTranscriptionText("deterministic transcript"));
        using var client = host.GetTestClient();
        using var content = CreateTranscriptionContent(LlmTckKnownModelIds.Gpt4OMiniTts);

        var response = await client.PostAsync("/openai/v1/audio/transcriptions", content);

        response.EnsureSuccessStatusCode();
        var trace = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot()
            .Single();

        await Assert.That(trace.ProviderId).IsEqualTo(LlmTckCompatibilityTags.OpenAI);
        await Assert.That(trace.ModelId).IsEqualTo(LlmTckKnownModelIds.Gpt4OMiniTts);
        await Assert.That(trace.OperationId)
            .IsEqualTo(LlmTckProviderOperationIds.OpenAI.AudioTranscriptionsCreate);
        await Assert.That(trace.RuntimeEventKind).IsEqualTo(LlmTckEventKind.Matched);
        await Assert.That(trace.RequestPreview).Contains("[multipart body summary;");
        await Assert.That(trace.RequestPreview)
            .Contains($"field model: {LlmTckKnownModelIds.Gpt4OMiniTts}");
        await Assert.That(trace.ResponsePreview).Contains("deterministic transcript");
    }

    [Test]
    public async Task ProviderRequestTrace_CapturesStreamingChunksAndRendersInspectorAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "trace-streaming",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("stream response")
                    .Responds("blue whale", "blue ", "whale")
            ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                stream = true,
                messages = new[] { new { role = "user", content = "stream response" } },
            },
            _jsonOptions
        );
        var responseBody = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        var trace = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot()
            .Single();
        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);
        var chunkSectionIndex = page.IndexOf("Stream chunks", StringComparison.Ordinal);
        var firstChunkIndex = page.IndexOf("blue ", chunkSectionIndex, StringComparison.Ordinal);
        var secondChunkIndex = page.IndexOf("whale", firstChunkIndex + "blue ".Length, StringComparison.Ordinal);

        await Assert.That(responseBody).Contains("data: ");
        await Assert.That(responseBody).Contains("data: [DONE]");
        await Assert.That(trace.IsStreaming).IsTrue();
        await Assert.That(trace.OperationId)
            .IsEqualTo(LlmTckProviderOperationIds.OpenAI.ChatCompletionsCreate);
        await Assert.That(trace.StreamChunks.Count).IsEqualTo(2);
        await Assert.That(trace.StreamChunks[0]).IsEqualTo("blue ");
        await Assert.That(trace.StreamChunks[1]).IsEqualTo("whale");
        await Assert.That(trace.ResponsePreview).Contains("data: ");
        await Assert.That(trace.ResponsePreview).Contains("[DONE]");
        await Assert.That(page).Contains("2 ordered chunks");
        await Assert.That(CountOccurrences(page, "class=\"chunk\"")).IsEqualTo(2);
        await Assert.That(chunkSectionIndex).IsGreaterThan(0);
        await Assert.That(firstChunkIndex).IsGreaterThan(chunkSectionIndex);
        await Assert.That(secondChunkIndex).IsGreaterThan(firstChunkIndex);
    }

    [Test]
    public async Task ProviderRequestTrace_RedactsNestedCredentialsAndPreservesUsageFieldsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "trace-redaction",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("redact credentials")
                    .Responds("safe response")
            ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                max_tokens = 321,
                refresh_token = "refresh-secret-value",
                metadata = new
                {
                    github_token = "github-secret-value",
                    private_key = "private-secret-value",
                    nested = new { session_token = "session-secret-value" },
                },
                messages = new[] { new { role = "user", content = "redact credentials" } },
            },
            _jsonOptions
        );

        response.EnsureSuccessStatusCode();
        var trace = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot()
            .Single();
        var preview = trace.RequestPreview
            ?? throw new InvalidOperationException("Expected a captured JSON request preview.");
        using var document = JsonDocument.Parse(preview);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");

        await Assert.That(trace.RequestPreviewTruncated).IsFalse();
        await Assert.That(root.GetProperty("max_tokens").GetInt32()).IsEqualTo(321);
        await Assert.That(root.GetProperty("refresh_token").GetString()).IsEqualTo("[redacted]");
        await Assert.That(metadata.GetProperty("github_token").GetString()).IsEqualTo("[redacted]");
        await Assert.That(metadata.GetProperty("private_key").GetString()).IsEqualTo("[redacted]");
        await Assert.That(metadata.GetProperty("nested").GetProperty("session_token").GetString())
            .IsEqualTo("[redacted]");
        await Assert.That(preview).DoesNotContain("refresh-secret-value");
        await Assert.That(preview).DoesNotContain("github-secret-value");
        await Assert.That(preview).DoesNotContain("private-secret-value");
        await Assert.That(preview).DoesNotContain("session-secret-value");
    }

    [Test]
    public async Task ProviderRequestTraceStore_RetainsNewestEntriesWithinConfiguredBoundAsync()
    {
        const int MaxEntries = 3;
        var store = new LlmTckProviderHttpTraceStore(
            Options.Create(new LlmTckProviderHttpTraceOptions { MaxEntries = MaxEntries })
        );

        for (var index = 1; index <= MaxEntries + 1; index++)
        {
            store.Start(new LlmTckProviderHttpTrace { RequestId = $"request-{index}" });
        }

        var snapshot = store.GetSnapshot();

        await Assert.That(snapshot.Count).IsEqualTo(MaxEntries);
        await Assert.That(snapshot[0].RequestId).IsEqualTo("request-4");
        await Assert.That(snapshot[1].RequestId).IsEqualTo("request-3");
        await Assert.That(snapshot[2].RequestId).IsEqualTo("request-2");
        await Assert.That(snapshot.Any(trace => trace.RequestId == "request-1")).IsFalse();
    }

    [Test]
    public async Task ProviderRequestTraceStore_BoundsRetainedBytesAndTruncatesOversizedTraceAsync()
    {
        const int MaxRetainedBytes = 2048;
        var store = new LlmTckProviderHttpTraceStore(
            Options.Create(
                new LlmTckProviderHttpTraceOptions
                {
                    MaxEntries = 10,
                    MaxPreviewBytes = 512,
                    MaxRetainedBytes = MaxRetainedBytes,
                }
            )
        );

        for (var index = 1; index <= 4; index++)
        {
            var requestId = $"request-{index}";
            store.Start(
                new LlmTckProviderHttpTrace
                {
                    RequestId = requestId,
                    ProviderId = LlmTckCompatibilityTags.OpenAI,
                    ProviderNamespace = LlmTckProviderRouteNamespaces.OpenAI,
                    Method = HttpMethod.Post.Method,
                    Path = "/openai",
                }
            );
            store.TryEnrich(
                requestId,
                new LlmTckProviderHttpTraceEnrichment
                {
                    Messages =
                    [
                        new LlmTckMessage
                        {
                            Role = "user",
                            Content = new string('m', 2000),
                        },
                    ],
                    RuntimeResponse = new string('r', 2000),
                }
            );
        }

        var snapshot = store.GetSnapshot();

        await Assert.That(store.RetainedByteCount).IsLessThanOrEqualTo(MaxRetainedBytes);
        await Assert.That(snapshot.Count).IsEqualTo(1);
        await Assert.That(snapshot[0].RequestId).IsEqualTo("request-4");
        await Assert.That(snapshot[0].RetentionTruncated).IsTrue();
        await Assert.That(snapshot[0].RetainedByteCount).IsLessThanOrEqualTo(MaxRetainedBytes);
        await Assert.That(snapshot[0].Messages.Single().Content.Length).IsLessThan(2000);
        await Assert.That(snapshot[0].RuntimeResponse).StartsWith(new string('r', 400));
        await Assert.That(snapshot[0].RuntimeResponse).EndsWith("…");
    }

    [Test]
    public async Task ProviderRequestTrace_CapturesHttpOnlyAndValidationFailureRequestsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var models = await client.GetAsync("/openai/v1/models");
        var invalid = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new { model = LlmTckKnownModelIds.Gpt41Mini },
            _jsonOptions
        );

        models.EnsureSuccessStatusCode();
        await Assert.That(invalid.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var traces = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot();
        var modelsTrace = traces.Single(trace => trace.Path.EndsWith("/models", StringComparison.Ordinal));
        var invalidTrace = traces.Single(trace => trace.Path.EndsWith("/chat/completions", StringComparison.Ordinal));

        await Assert.That(traces.Count).IsEqualTo(2);
        await Assert.That(modelsTrace.StatusCode).IsEqualTo((int)HttpStatusCode.OK);
        await Assert.That(modelsTrace.RuntimeEventKind).IsNull();
        await Assert.That(invalidTrace.StatusCode).IsEqualTo((int)HttpStatusCode.BadRequest);
        await Assert.That(invalidTrace.RuntimeEventKind).IsNull();
        await Assert.That(invalidTrace.ResponsePreview).Contains("invalid_request");
    }

    [Test]
    public async Task ProviderFallbacks_TraceUnknownAndWrongMethodRequestsAndRenderBothRowsAsync()
    {
        const string FallbackModel = "fallback-diagnostic-model";
        const string RequestPayload = "wrong method fallback payload";
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var missing = await client.GetAsync("/openai/v1/diagnostics/not-supported");
        var wrongMethod = await client.PostAsJsonAsync(
            "/openai/v1/models",
            new { model = FallbackModel, request = RequestPayload },
            _jsonOptions
        );

        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(wrongMethod.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
        await Assert.That(wrongMethod.Content.Headers.Allow.Single()).IsEqualTo("GET");

        var traces = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot();
        var missingTrace = traces.Single(trace =>
            trace.Path.EndsWith("/diagnostics/not-supported", StringComparison.Ordinal)
        );
        var wrongMethodTrace = traces.Single(trace =>
            trace.Path.EndsWith("/v1/models", StringComparison.Ordinal)
        );
        var preview = wrongMethodTrace.RequestPreview
            ?? throw new InvalidOperationException("Expected the fallback request payload to be captured.");
        using var previewDocument = JsonDocument.Parse(preview);
        var previewRoot = previewDocument.RootElement;

        await Assert.That(traces.Count).IsEqualTo(2);
        await Assert.That(missingTrace.ProviderId).IsEqualTo(LlmTckCompatibilityTags.OpenAI);
        await Assert.That(missingTrace.StatusCode).IsEqualTo((int)HttpStatusCode.NotFound);
        await Assert.That(missingTrace.TransportState)
            .IsEqualTo(LlmTckProviderHttpTraceTransportState.Completed);
        await Assert.That(missingTrace.OperationId).IsNull();
        await Assert.That(wrongMethodTrace.ProviderId).IsEqualTo(LlmTckCompatibilityTags.OpenAI);
        await Assert.That(wrongMethodTrace.Method).IsEqualTo(HttpMethod.Post.Method);
        await Assert.That(wrongMethodTrace.StatusCode)
            .IsEqualTo((int)HttpStatusCode.MethodNotAllowed);
        await Assert.That(wrongMethodTrace.TransportState)
            .IsEqualTo(LlmTckProviderHttpTraceTransportState.Completed);
        await Assert.That(wrongMethodTrace.OperationId).IsNull();
        await Assert.That(wrongMethodTrace.ModelId).IsEqualTo(FallbackModel);
        await Assert.That(previewRoot.GetProperty("model").GetString()).IsEqualTo(FallbackModel);
        await Assert.That(previewRoot.GetProperty("request").GetString()).IsEqualTo(RequestPayload);

        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);
        await Assert.That(CountOccurrences(page, "data-testid=\"request-row\"")).IsEqualTo(2);
        await Assert.That(page).Contains($"data-request-id=\"{missingTrace.RequestId}\"");
        await Assert.That(page).Contains($"data-request-id=\"{wrongMethodTrace.RequestId}\"");
        await Assert.That(page).Contains(FallbackModel);
        await Assert.That(page).Contains(RequestPayload);
    }

    [Test]
    public async Task AdminPanel_RendersScriptedErrorResponseAsProviderErrorAsync()
    {
        const string ErrorMessage = "Scripted provider refusal.";
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "provider-error-label",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("trigger provider error")
                    .Fails(400, "content_filter", ErrorMessage)
            ));
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                messages = new[] { new { role = "user", content = "trigger provider error" } },
            },
            _jsonOptions
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var trace = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot()
            .Single();
        await Assert.That(trace.RuntimeEventKind).IsEqualTo(LlmTckEventKind.ErrorReturned);
        await Assert.That(trace.RuntimeResponse).IsEqualTo(ErrorMessage);

        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);
        await Assert.That(page).Contains("<article class=\"turn error\"");
        await Assert.That(page).Contains("<div class=\"turn-role\">provider error</div>");
        await Assert.That(page).Contains(ErrorMessage);
    }

    [Test]
    public async Task ProviderRequestTrace_CorrelatesCacheWriteThenCacheReadPerRequestAsync()
    {
        var cacheablePrefix = string.Join(' ', Enumerable.Repeat("stable-prefix", 1200));
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "trace-cache",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("color")
                    .Responds("first response")
                    .Responds("second response")
            ));
        using var client = host.GetTestClient();
        var request = new
        {
            model = LlmTckKnownModelIds.Gpt41Mini,
            messages = new[]
            {
                new { role = "system", content = cacheablePrefix },
                new { role = "user", content = "What color is the whale?" },
            },
        };

        (await client.PostAsJsonAsync("/openai/v1/chat/completions", request, _jsonOptions))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/openai/v1/chat/completions", request, _jsonOptions))
            .EnsureSuccessStatusCode();

        var traces = host.Services
            .GetRequiredService<ILlmTckProviderHttpTraceStore>()
            .GetSnapshot();
        var second = traces[0];
        var first = traces[1];
        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);

        await Assert.That(traces.Count).IsEqualTo(2);
        await Assert.That(first.Usage).IsNotNull();
        await Assert.That(second.Usage).IsNotNull();
        await Assert.That(first.Usage!.CachedInputTokens).IsEqualTo(0);
        await Assert.That(first.Usage.CacheCreationInputTokens).IsGreaterThanOrEqualTo(1024);
        await Assert.That(second.Usage!.CachedInputTokens).IsGreaterThanOrEqualTo(1024);
        await Assert.That(second.Usage.CacheCreationInputTokens).IsEqualTo(0);
        await Assert.That(second.RequestId).IsNotEqualTo(first.RequestId);
        await Assert.That(CountOccurrences(page, "data-testid=\"request-row\"")).IsEqualTo(2);
        await Assert.That(page).Contains($"data-request-id=\"{first.RequestId}\"");
        await Assert.That(page).Contains($"data-request-id=\"{second.RequestId}\"");
        await Assert.That(first.RuntimeResponse).IsEqualTo("first response");
        await Assert.That(second.RuntimeResponse).IsEqualTo("second response");
    }

    [Test]
    public async Task AdminPanel_RendersOneRequestRowWithFullConversationAndExchangeAsync()
    {
        var longSystemMessage = $"{new string('z', 900)} END_OF_LONG_PROMPT";
        using var host = await LlmTckTestHost.StartAsync(options => options.AddChatScenario(
                "dashboard-multi-turn",
                scenario => scenario
                    .ForModel(LlmTckKnownModelIds.Gpt41Mini)
                    .WhenUserContains("color")
                    .Responds("blue whale")
            ));
        using var client = host.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = LlmTckKnownModelIds.Gpt41Mini,
                messages = new[]
                {
                    new { role = "system", content = longSystemMessage },
                    new { role = "user", content = "Previous question" },
                    new { role = "assistant", content = "Previous answer" },
                    new { role = "user", content = "What color is the whale?" },
                },
            },
            _jsonOptions
        );
        response.EnsureSuccessStatusCode();

        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);
        var conversationIndex = page.IndexOf("data-testid=\"conversation\"", StringComparison.Ordinal);
        var responseIndex = page.IndexOf("data-response-card=\"true\"", StringComparison.Ordinal);
        var collapsedSystemIndex = page.IndexOf(
            "data-collapsed-by-default=\"true\"",
            StringComparison.Ordinal
        );

        await Assert.That(CountOccurrences(page, "data-testid=\"request-row\"")).IsEqualTo(1);
        await Assert.That(CountOccurrences(page, "data-testid=\"conversation-turn\"")).IsEqualTo(5);
        await Assert.That(page).Contains("OpenAI");
        await Assert.That(page).Contains(LlmTckKnownModelIds.Gpt41Mini);
        await Assert.That(page).Contains("Request metadata");
        await Assert.That(page).Contains("Tokens & cache");
        await Assert.That(page).Contains("Wire payloads");
        await Assert.That(page).Contains("END_OF_LONG_PROMPT");
        await Assert.That(page).Contains("Previous answer");
        await Assert.That(page).Contains("blue whale");
        await Assert.That(page).Contains("aria-selected=\"true\"");
        await Assert.That(page).Contains("aria-selected=\"false\"");
        await Assert.That(page).Contains("aria-pressed=\"true\"");
        await Assert.That(page).Contains("aria-pressed=\"false\"");
        await Assert.That(page).Contains("role=\"tabpanel\"");
        await Assert.That(conversationIndex).IsGreaterThan(0);
        await Assert.That(responseIndex).IsGreaterThan(conversationIndex);
        await Assert.That(collapsedSystemIndex).IsGreaterThan(responseIndex);
        await Assert.That(page.IndexOf("END_OF_LONG_PROMPT", conversationIndex, StringComparison.Ordinal))
            .IsGreaterThan(conversationIndex);
    }

    [Test]
    public async Task AdminPanel_PrettyPrintsJsonWithoutUnicodeEscapeNoiseAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/openai/v1/chat/completions",
            new
            {
                model = "missing-model",
                messages = new[] { new { role = "user", content = "Show the error payload" } },
            },
            _jsonOptions
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);

        await Assert.That(page).Contains("data-testid=\"overview-response-json\"");
        await Assert.That(page).Contains("&quot;error&quot;");
        await Assert.That(page).Contains("missing-model");
        await Assert.That(page).Contains("provider error");
        await Assert.That(page).DoesNotContain("\\u0027");
    }

    [Test]
    public async Task AdminPanel_LabelsNonChatJsonAsProviderResponseAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();

        (await client.GetAsync("/openai/v1/models")).EnsureSuccessStatusCode();
        var page = await client.GetStringAsync(LlmTckControlRoutes.Admin);

        await Assert.That(page).Contains("provider response");
        await Assert.That(page).DoesNotContain(">assistant</span>");
    }

    [Test]
    public async Task ResetControlRoute_ClearsProviderRequestJournalAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();
        var store = host.Services.GetRequiredService<ILlmTckProviderHttpTraceStore>();

        (await client.GetAsync("/openai/v1/models")).EnsureSuccessStatusCode();
        await Assert.That(store.GetSnapshot().Count).IsEqualTo(1);

        (await client.PostAsync(LlmTckControlRoutes.Reset, content: null)).EnsureSuccessStatusCode();
        await Assert.That(store.GetSnapshot()).IsEmpty();
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static MultipartFormDataContent CreateTranscriptionContent(string model)
    {
        var content = new MultipartFormDataContent();
        var audio = new ByteArrayContent(Encoding.ASCII.GetBytes("RIFF....WAVE"));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audio, "file", "fixture.wav");
        content.Add(new StringContent(model), "model");
        content.Add(new StringContent("json"), "response_format");
        return content;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ManagedCode.LlmTck.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find ManagedCode.LlmTck.slnx.");
    }
}

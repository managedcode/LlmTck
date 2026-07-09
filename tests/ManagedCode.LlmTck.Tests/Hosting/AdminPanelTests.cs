using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Tests.TestSupport;

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
        await Assert.That(body).Contains("LLM&nbsp;TCK");
        await Assert.That(body).Contains(LlmTckControlRoutes.Models);
        await Assert.That(body).Contains(LlmTckControlRoutes.Assertions);
        await Assert.That(body).Contains("Fixtures / models");
        await Assert.That(body).Contains("Actions");
        await Assert.That(body).Contains("API endpoints");
        await Assert.That(body).Contains("Control API");
        await Assert.That(body).Contains("Provider APIs");
        await Assert.That(body).Contains("Grouped overview");
        await Assert.That(body).Contains("Calls / requests");
        await Assert.That(body).Contains("Responses");
        await Assert.That(body).Contains("blazor.web.js");
        await Assert.That(body).DoesNotContain("setInterval(refresh, 4000)");
        await Assert.That(body).DoesNotContain("setTimeout(function ()");
        await Assert.That(body).DoesNotContain("autoCountdown");
        await Assert.That(body).Contains("Total tokens");
        await Assert.That(body).Contains("Cached input");
        await Assert.That(body).Contains("Cache writes");
        await Assert.That(body).Contains("Reasoning tokens");
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
        await Assert.That(endpointSource).Contains("MapRazorComponents<LlmTckAdminPage>");
        await Assert.That(endpointSource).Contains("MapStaticAssets");
        await Assert.That(endpointSource).Contains("StaticWebAssetsLoader.UseStaticWebAssets");
        await Assert.That(endpointSource).Contains("catch (DirectoryNotFoundException exception)");
        await Assert.That(endpointSource).Contains("Skipping LLM TCK static web assets runtime manifest");
        await Assert.That(endpointSource).Contains("AddInteractiveServerComponents");
        await Assert.That(endpointSource).Contains("AddInteractiveServerRenderMode");
        await Assert.That(endpointSource).Contains("DisableAntiforgery");
        await Assert.That(razorComponent).Contains("@page \"/\"");
        await Assert.That(razorComponent).Contains("InteractiveServerRenderMode");
        await Assert.That(razorComponent).Contains("LlmTckAdminPanel");
        await Assert.That(panelComponent).Contains("PeriodicTimer");
        await Assert.That(panelComponent).Contains("StateHasChanged");
        await Assert.That(panelComponent).Contains("tokens:");
        await Assert.That(panelComponent).Contains("cache write");
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

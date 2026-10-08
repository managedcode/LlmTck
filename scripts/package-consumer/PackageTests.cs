using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using ManagedCode.LlmTck.Aspire;
using Microsoft.Playwright;
using TUnit.Core;
using static Microsoft.Playwright.Assertions;

public sealed class PackageTests
{
    [Test]
    public async Task InstalledPackage_ServesAssetsAndInteractiveDashboard(CancellationToken cancellationToken)
    {
        var builder = DistributedApplicationTestingBuilder.Create([]);
        var resource = builder.AddLlmTck("package-tck").WithApiKey("test-key");
        if (!resource.Resource.WorkingDirectory.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal))
            throw new InvalidOperationException("The service must come from the installed package output.");
        if (Directory.GetFiles(resource.Resource.WorkingDirectory, "*.staticwebassets.runtime.json", SearchOption.AllDirectories).Length != 0)
            throw new InvalidOperationException("Package contains a build-machine static assets manifest.");
        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("package-tck", cancellationToken);
        using var http = app.CreateHttpClient("package-tck", "http");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-key");
        foreach (var path in new[] { "/health", "/admin-api/models", "/_framework/blazor.web.js", "/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js" })
        {
            using var response = await http.GetAsync(path, cancellationToken);
            response.EnsureSuccessStatusCode();
            var length = (await response.Content.ReadAsByteArrayAsync(cancellationToken)).Length;
            Console.WriteLine($"{path}: {(int)response.StatusCode}, {length} bytes");
            if (path.EndsWith(".js", StringComparison.Ordinal) && length < 100)
                throw new InvalidOperationException($"Missing JavaScript asset: {path}");
        }
        using (var configuration = new StringContent("""
            {"requiredBearerToken":"test-key","models":[{"id":"gpt-4.1-mini","kind":"chat"}],
             "chatScenarios":[{"id":"package-tools","responses":[{"toolCalls":[{"id":"call_package","name":"weather","argumentsJson":"{\"city\":\"Paris\"}"}]}]}]}
            """, Encoding.UTF8, "application/json"))
        using (var configured = await http.PostAsync("/admin-api/configure", configuration, cancellationToken))
            configured.EnsureSuccessStatusCode();
        using (var request = new StringContent("""
            {"model":"gpt-4.1-mini","messages":[{"role":"user","content":"weather"}],
             "tools":[{"type":"function","function":{"name":"weather","parameters":{"type":"object","required":["city"],"properties":{"city":{"const":"Paris"}}}}}],"tool_choice":"required"}
            """, Encoding.UTF8, "application/json"))
        using (var response = await http.PostAsync("/openai/v1/chat/completions", request, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var function = payload.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("tool_calls")[0].GetProperty("function");
            if (function.GetProperty("name").GetString() != "weather" || function.GetProperty("arguments").GetString() != "{\"city\":\"Paris\"}")
                throw new InvalidOperationException("Installed package lost the schema-validated tool fixture.");
        }
        // Verify the published Aspire service actually includes every native decision adapter.
        using (var configuration = new StringContent("""
            {"requiredBearerToken":"test-key","models":[{"id":"kev-latest","kind":"decision","decisionProvider":"kev"},{"id":"clef","kind":"decision","decisionProvider":"cloudflare"},{"id":"gpt-6-luna","kind":"decision","decisionProvider":"openAI"}],
             "decisionScenarios":[{"id":"kev","modelId":"kev-latest","answers":{"binary":{"kind":"predicate","probability":0.95}}},
              {"id":"clef","modelId":"clef","answers":{"binary":{"kind":"predicate","probability":0.95}}},
              {"id":"openai","modelId":"gpt-6-luna","answers":{"0":{"kind":"predicate","probability":0.95}}}]}
            """, Encoding.UTF8, "application/json"))
        using (var configured = await http.PostAsync("/admin-api/configure", configuration, cancellationToken))
            configured.EnsureSuccessStatusCode();
        foreach (var (path, model) in new[] { ("/systemone/v1/systemone", "kev-latest"), ("/cloudflare/client/v4/accounts/test/ai/run/@cf/cloudflare/clef", "clef"), ("/openai/v1/decisions", "gpt-6-luna") })
        {
            var body = model == "gpt-6-luna"
                ? """{"model":"gpt-6-luna","input":"evidence","questions":[{"type":"predicate","name":"binary","instructions":"binary?"}]}"""
                : """{"model":"MODEL","state":"evidence","questions":{"binary":{"type":"noul","instructions":"binary?"}}}""".Replace("MODEL", model, StringComparison.Ordinal);
            if (model == "clef")
            {
                const string image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNgYGAAAAAEAAH2FzhVAAAAAElFTkSuQmCC";
                var native = JsonNode.Parse(body)!.AsObject(); native["images"] = new JsonArray(image); body = native.ToJsonString();
            }
            using var request = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (!(await response.Content.ReadAsStringAsync(cancellationToken)).Contains("0.95", StringComparison.Ordinal))
                throw new InvalidOperationException("Installed package lost the native decision fixture: " + model);
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(http.BaseAddress!.ToString());
        await page.GetByRole(AriaRole.Button, new() { Name = "Runtime settings" }).ClickAsync();
        await Expect(page.GetByLabel("Bearer token", new() { Exact = true })).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = "package-dashboard.png", FullPage = true });
        await page.GetByLabel("Bearer token", new() { Exact = true }).FillAsync("test-key");
        await page.GetByRole(AriaRole.Button, new() { Name = "Close runtime settings", Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("Bearer token", new() { Exact = true })).Not.ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Refresh runtime state" }).ClickAsync();
        await Expect(page.GetByText("Unauthorized", new() { Exact = true })).ToHaveCountAsync(0);
        await page.ScreenshotAsync(new() { Path = "package-dashboard-authorized.png", FullPage = true });
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

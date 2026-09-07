using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class ReviewVideoRegressionTests
{
    [Test]
    [Arguments("/openai/v1/videos", "", false)]
    [Arguments("/azure-openai/openai/v1/video/generations/jobs", "?api-version=preview", true)]
    public async Task VideoLifecycle_PersistsCompletedJobsAndDeletesContentAsync(string path, string query, bool azure)
    {
        using var host = await LlmTckTestHost.StartAsync();
        using var client = host.GetTestClient();
        using var unknown = await client.GetAsync(path + "/never-created" + query);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        using var first = await client.PostAsJsonAsync(path + query, new { model = "sora-2", prompt = "whale", seconds = "8", n_seconds = 8 });
        first.EnsureSuccessStatusCode();
        var created = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();
        await Assert.That(created.GetProperty("status").GetString()).IsEqualTo(azure ? "succeeded" : "completed");
        if (!azure)
        {
            await Assert.That(created.GetProperty("progress").GetInt32()).IsEqualTo(100);
        }

        using var retrieved = await client.GetAsync($"{path}/{id}{query}");
        var stored = await retrieved.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(stored.GetProperty("prompt").GetString()).IsEqualTo("whale");
        if (azure)
        {
            await Assert.That(stored.GetProperty("n_seconds").GetInt32()).IsEqualTo(8);
        }
        else
        {
            await Assert.That(stored.GetProperty("seconds").GetString()).IsEqualTo("8");
        }

        using var second = await client.PostAsJsonAsync(path + query, new { model = "sora-2", prompt = "second" });
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        await Assert.That(secondId).IsNotEqualTo(id);
        using var listed = await client.GetAsync(path + query);
        await Assert.That((await listed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetArrayLength()).IsEqualTo(2);
        var contentPath = azure
            ? $"/azure-openai/openai/v1/video/generations/{created.GetProperty("generations")[0].GetProperty("id").GetString()}/content/video{query}"
            : $"{path}/{id}/content";
        using var content = await client.GetAsync(contentPath);
        content.EnsureSuccessStatusCode();
        await Assert.That((await content.Content.ReadAsByteArrayAsync()).Length).IsGreaterThan(0);
        using var deleted = await client.DeleteAsync($"{path}/{id}{query}");
        deleted.EnsureSuccessStatusCode();
        using var afterDelete = await client.GetAsync($"{path}/{id}{query}");
        using var deletedContent = await client.GetAsync(contentPath);
        using var repeatedDelete = await client.DeleteAsync($"{path}/{id}{query}");
        await Assert.That(afterDelete.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(deletedContent.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(repeatedDelete.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await host.Services.GetRequiredService<ILlmTckRuntime>().ResetAsync();
        using var afterReset = await client.GetAsync($"{path}/{secondId}{query}");
        await Assert.That(afterReset.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task VideoStore_IsolatesProvidersSnapshotsBytesAndClearsOnConfigureAsync()
    {
        var runtime = new LlmTckRuntime();
        var generated = await runtime.GenerateVideoAsync("sora-2", "whale");
        var stored = runtime.StoreVideo(LlmTckCompatibilityTags.OpenAI, generated);
        var firstByte = stored.Bytes[0];
        stored.Bytes[0] = 255;
        await Assert.That(runtime.FindVideo(LlmTckCompatibilityTags.OpenAI, stored.VideoId)!.Bytes[0]).IsEqualTo(firstByte);
        await Assert.That(runtime.FindVideo("azure", stored.VideoId)).IsNull();
        await runtime.ConfigureAsync(LlmTckConfiguration.CreateDefault());
        await Assert.That(runtime.ListVideos(LlmTckCompatibilityTags.OpenAI).Count).IsEqualTo(0);
    }
}

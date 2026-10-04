#pragma warning disable OPENAI001 // Official Responses client adapter is still marked experimental.

using System.ClientModel;
using System.ClientModel.Primitives;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

namespace ManagedCode.LlmTck.Tests.Azure;

public sealed class LlmTckResponsesContinuationTests
{
    private const string _key = "continuation-test-key";
    private const string _prompt = "review-file-continuation";
    private const string _evidence = "private-document-evidence-417";
    private const string _toolName = "read_document";

    [Test]
    [Arguments(LlmTckProviderRouteNamespaces.OpenAI, false)]
    [Arguments(LlmTckProviderRouteNamespaces.OpenAI, true)]
    [Arguments(LlmTckProviderRouteNamespaces.AzureOpenAI, false)]
    [Arguments(LlmTckProviderRouteNamespaces.AzureOpenAI, true)]
    [Arguments(LlmTckProviderRouteNamespaces.MicrosoftFoundry, false)]
    [Arguments(LlmTckProviderRouteNamespaces.MicrosoftFoundry, true)]
    public async Task OfficialMeaiToolLoop_RestoresPreviousResponseContextAsync(string provider, bool streaming)
    {
        using var host = await LlmTckTestHost.StartAsync(options => options.RequireBearerToken(_key)
            .AddModel(LlmTckKnownModelIds.Gpt41Mini, LlmTckModelKind.Chat)
            .AddChatScenario(_prompt, scenario => scenario.ForModel(LlmTckKnownModelIds.Gpt41Mini)
                .WhenUserContains(_prompt).CallsTool(_toolName, "{}", "call_document").Responds("reviewed")));
        using var http = host.GetTestClient();
        var apiRoot = provider == LlmTckProviderRouteNamespaces.OpenAI ? "/v1/" : "/openai/v1/";
        var sdk = new ResponsesClient(new ApiKeyCredential(_key), new ResponsesClientOptions
        {
            Endpoint = new Uri(http.BaseAddress!, provider.TrimStart('/') + apiRoot),
            Transport = new HttpClientPipelineTransport(http)
        });
        using var client = sdk.AsIChatClient(LlmTckKnownModelIds.Gpt41Mini).AsBuilder().UseFunctionInvocation().Build();
        var chatOptions = new ChatOptions { Tools = [AIFunctionFactory.Create(() => _evidence, _toolName)] };
        var picture = new LlmTckConfiguration().DefaultImageDataUri;
        ChatMessage[] input = [new(ChatRole.User, [new TextContent(_prompt),
            new DataContent(Convert.FromBase64String(picture[(picture.IndexOf(',') + 1)..]), "image/png")])];
        if (streaming)
        {
            var text = string.Empty;
            await foreach (var update in client.GetStreamingResponseAsync(input, chatOptions)) { text += update.Text; }
            await Assert.That(text).IsEqualTo("reviewed");
        }
        else
        {
            await Assert.That((await client.GetResponseAsync(input, chatOptions)).Text).IsEqualTo("reviewed");
        }
        var events = host.Services.GetRequiredService<ILlmTckRuntime>().GetAssertionSummary().Events;
        await Assert.That(events.Count).IsEqualTo(2);
        await Assert.That(events.All(item => item.Kind == LlmTckEventKind.Matched)).IsTrue();
        await Assert.That(events.Last().Messages.Any(message => message.Role == "user" && message.Content.Contains(_prompt, StringComparison.Ordinal) && message.Content.Contains(picture, StringComparison.Ordinal))).IsTrue();
        await Assert.That(events.Last().Messages.Single(message => message.Role == "tool").Content).Contains(_evidence);
    }
}

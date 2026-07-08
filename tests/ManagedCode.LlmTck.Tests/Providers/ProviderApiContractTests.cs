using ManagedCode.LlmTck.Anthropic;
using ManagedCode.LlmTck.AzureOpenAI;
using ManagedCode.LlmTck.Bedrock;
using ManagedCode.LlmTck.Cohere;
using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.DeepSeek;
using ManagedCode.LlmTck.Foundry;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Groq;
using ManagedCode.LlmTck.Mistral;
using ManagedCode.LlmTck.Ollama;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.OpenRouter;
using ManagedCode.LlmTck.Perplexity;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Tests.TestSupport;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class ProviderApiContractTests
{
    private const int _maxDocumentationReviewAgeDays = 180;

    private static readonly Uri[] _officialDocumentationHosts =
    [
        new("https://developers.openai.com"),
        new("https://learn.microsoft.com"),
        new("https://docs.anthropic.com"),
        new("https://ai.google.dev"),
        new("https://console.groq.com"),
        new("https://docs.mistral.ai"),
        new("https://docs.ollama.com"),
        new("https://github.com/ollama/ollama"),
        new("https://docs.cohere.com"),
        new("https://docs.aws.amazon.com"),
        new("https://openrouter.ai"),
        new("https://api-docs.deepseek.com"),
        new("https://docs.perplexity.ai"),
    ];

    [Test]
    public async Task ProviderApiContracts_AreDocBackedAndCoverClaimedCapabilitiesAsync()
    {
        foreach (var profile in GetProfiles())
        {
            await Assert.That(IsOfficialDocumentationUrl(profile.ApiContract.DocumentationUrl)).IsTrue();
            var hasDocumentationDate = DateOnly.TryParse(
                profile.ApiContract.DocumentationRetrievedOn,
                out var documentationRetrievedOn
            );

            await Assert.That(hasDocumentationDate).IsTrue();
            await Assert.That(IsFreshDocumentationReview(documentationRetrievedOn)).IsTrue();
            await Assert.That(profile.ApiContract.DocumentationVersion).IsNotNull();
            await Assert.That(profile.ApiContract.DocumentationVersion).IsNotEmpty();
            await Assert.That(profile.ApiContract.Operations).IsNotEmpty();

            var documentedCapabilities = profile.ApiContract.Operations
                .SelectMany(operation => operation.Capabilities)
                .Distinct()
                .ToArray();
            var duplicateOperations = profile.ApiContract.Operations
                .GroupBy(operation => new { operation.Method, operation.Path })
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.Method} {group.Key.Path}")
                .ToArray();
            var unimplementedOperations = profile.ApiContract.Operations
                .Where(operation => !operation.ImplementedByHosting)
                .Select(operation => operation.Id)
                .ToArray();

            await Assert.That(profile.Capabilities.Except(documentedCapabilities)).IsEmpty();
            await Assert.That(duplicateOperations).IsEmpty();
            await Assert.That(unimplementedOperations).IsEmpty();
            await Assert.That(
                    profile.ApiContract.Operations.Any(operation =>
                        string.Equals(
                            operation.Path,
                            profile.DefaultEndpointPath,
                            StringComparison.Ordinal
                        )
                    ) || profile.ApiContract.Operations.Any(operation =>
                        operation.Path.StartsWith(profile.DefaultEndpointPath, StringComparison.Ordinal)
                    )
                )
                .IsTrue();

            foreach (var operation in profile.ApiContract.Operations)
            {
                await Assert.That(operation.Id).IsNotEmpty();
                await Assert.That(operation.Path).StartsWith("/");
                await Assert.That(IsSupportedHttpMethod(operation.Method)).IsTrue();
                await Assert.That(IsOfficialDocumentationUrl(operation.DocumentationUrl)).IsTrue();

                if (operation.SupportsStreaming)
                {
                    await Assert.That(
                            operation.Capabilities.Any(capability =>
                                capability
                                    is LlmTckProviderCapability.StreamingChat
                                        or LlmTckProviderCapability.StreamingImages
                                        or LlmTckProviderCapability.StreamingAudio
                            )
                        )
                        .IsTrue();
                }
            }
        }
    }

    [Test]
    public async Task HostingProviderRoutes_AreCoveredByImplementedDocumentedOperationsAsync()
    {
        using var host = await LlmTckTestHost.StartAsync();
        var routes = host.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    ?? ["GET"];

                return methods.Select(method => new ProviderRoute(
                    method,
                    endpoint.RoutePattern.RawText ?? string.Empty
                ));
            })
            .Where(route => !route.Path.StartsWith(LlmTckControlRoutes.Admin, StringComparison.Ordinal))
            .OrderBy(route => route.Method, StringComparer.Ordinal)
            .ThenBy(route => route.Path, StringComparer.Ordinal)
            .ToArray();

        var implementedOperations = GetProfiles()
            .SelectMany(profile => profile.ApiContract.Operations)
            .Where(operation => operation.ImplementedByHosting)
            .Select(operation => new ProviderRoute(operation.Method, operation.Path))
            .Distinct()
            .OrderBy(route => route.Method, StringComparer.Ordinal)
            .ThenBy(route => route.Path, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(routes).IsEquivalentTo(implementedOperations);
    }

    [Test]
    public async Task ProviderApiContracts_UseExplicitProviderNamespacesAsync()
    {
        foreach (var profile in GetProfiles())
        {
            var expectedNamespace = GetExpectedProviderNamespace(profile.Id);

            await Assert.That(profile.DefaultEndpointPath).StartsWith(expectedNamespace);

            foreach (var operation in profile.ApiContract.Operations)
            {
                await Assert.That(operation.Path).StartsWith(expectedNamespace);
            }
        }
    }

    private static bool IsFreshDocumentationReview(DateOnly retrievedOn)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var oldestAccepted = today.AddDays(-_maxDocumentationReviewAgeDays);
        return retrievedOn >= oldestAccepted && retrievedOn <= today;
    }

    private static bool IsOfficialDocumentationUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && _officialDocumentationHosts.Any(host =>
                string.Equals(uri.Host, host.Host, StringComparison.OrdinalIgnoreCase)
            );
    }

    private static bool IsSupportedHttpMethod(string method)
    {
        return method is "DELETE" or "GET" or "HEAD" or "POST";
    }

    private static string GetExpectedProviderNamespace(string providerId)
    {
        return providerId switch
        {
            LlmTckCompatibilityTags.OpenAI => LlmTckProviderRouteNamespaces.OpenAI,
            LlmTckCompatibilityTags.AzureOpenAI => LlmTckProviderRouteNamespaces.AzureOpenAI,
            LlmTckCompatibilityTags.MicrosoftFoundry => LlmTckProviderRouteNamespaces.MicrosoftFoundry,
            LlmTckCompatibilityTags.Anthropic => LlmTckProviderRouteNamespaces.Anthropic,
            LlmTckCompatibilityTags.Gemini => LlmTckProviderRouteNamespaces.Gemini,
            LlmTckCompatibilityTags.Groq => LlmTckProviderRouteNamespaces.Groq,
            LlmTckCompatibilityTags.Mistral => LlmTckProviderRouteNamespaces.Mistral,
            LlmTckCompatibilityTags.Ollama => LlmTckProviderRouteNamespaces.Ollama,
            LlmTckCompatibilityTags.Cohere => LlmTckProviderRouteNamespaces.Cohere,
            LlmTckCompatibilityTags.Bedrock => LlmTckProviderRouteNamespaces.Bedrock,
            LlmTckCompatibilityTags.OpenRouter => LlmTckProviderRouteNamespaces.OpenRouter,
            LlmTckCompatibilityTags.DeepSeek => LlmTckProviderRouteNamespaces.DeepSeek,
            LlmTckCompatibilityTags.Perplexity => LlmTckProviderRouteNamespaces.Perplexity,
            _ => throw new InvalidOperationException($"Unknown provider '{providerId}'."),
        };
    }

    private static LlmTckProviderProfile[] GetProfiles()
    {
        return
        [
            OpenAiCompatibility.Profile,
            AzureOpenAiCompatibility.Profile,
            MicrosoftFoundryCompatibility.Profile,
            AnthropicCompatibility.Profile,
            GeminiCompatibility.Profile,
            GroqCompatibility.Profile,
            MistralCompatibility.Profile,
            OllamaCompatibility.Profile,
            CohereCompatibility.Profile,
            BedrockCompatibility.Profile,
            OpenRouterCompatibility.Profile,
            DeepSeekCompatibility.Profile,
            PerplexityCompatibility.Profile,
        ];
    }

    private sealed record ProviderRoute(string Method, string Path);
}

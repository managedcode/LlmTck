using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ManagedCode.LlmTck.Aspire;

public static class LlmTckAspireExtensions
{
    public static IResourceBuilder<LlmTckResource> AddLlmTck(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name = LlmTckResource.DefaultName
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = new LlmTckResource(name);

        return ConfigureLlmTckResource(builder.AddResource(resource), name)
            .WithImage(LlmTckContainerImageTags.Image, LlmTckContainerImageTags.Tag)
            .WithImageRegistry(LlmTckContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: LlmTckResource.HttpPort, name: "http")
            .WithHttpHealthCheck("/");
    }

    public static IResourceBuilder<TResource> WithOpenAICompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "OPENAI", enabled);
    }

    public static IResourceBuilder<TResource> WithAzureOpenAICompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "AZURE_OPENAI", enabled);
    }

    public static IResourceBuilder<TResource> WithFoundryCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "MICROSOFT_FOUNDRY", enabled);
    }

    public static IResourceBuilder<TResource> WithAnthropicCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "ANTHROPIC", enabled);
    }

    public static IResourceBuilder<TResource> WithGeminiCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "GEMINI", enabled);
    }

    public static IResourceBuilder<TResource> WithGroqCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "GROQ", enabled);
    }

    public static IResourceBuilder<TResource> WithMistralCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "MISTRAL", enabled);
    }

    public static IResourceBuilder<TResource> WithOllamaCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "OLLAMA", enabled);
    }

    public static IResourceBuilder<TResource> WithCohereCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "COHERE", enabled);
    }

    public static IResourceBuilder<TResource> WithBedrockCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "BEDROCK", enabled);
    }

    public static IResourceBuilder<TResource> WithOpenRouterCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "OPENROUTER", enabled);
    }

    public static IResourceBuilder<TResource> WithDeepSeekCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "DEEPSEEK", enabled);
    }

    public static IResourceBuilder<TResource> WithPerplexityCompatibility<TResource>(
        this IResourceBuilder<TResource> builder,
        bool enabled = true
    )
        where TResource : IResourceWithEnvironment
    {
        return WithCompatibility(builder, "PERPLEXITY", enabled);
    }

    public static IResourceBuilder<TResource> WithEndpoint<TResource>(
        this IResourceBuilder<TResource> builder,
        string endpoint
    )
        where TResource : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || string.IsNullOrWhiteSpace(endpointUri.Host)
        )
        {
            throw new ArgumentException("Endpoint must be an absolute URI.", nameof(endpoint));
        }

        return builder
            .WithEnvironment("LlmTck__Endpoint", endpointUri.AbsoluteUri)
            .WithEnvironment("LLM_TCK_ENDPOINT", endpointUri.AbsoluteUri);
    }

    public static IResourceBuilder<TResource> WithApiKey<TResource>(
        this IResourceBuilder<TResource> builder,
        string apiKey
    )
        where TResource : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return builder.WithEnvironment("LlmTck__RequiredBearerToken", apiKey);
    }

    private static IResourceBuilder<TResource> WithCompatibility<TResource>(
        IResourceBuilder<TResource> builder,
        string provider,
        bool enabled
    )
        where TResource : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return builder.WithEnvironment(
            $"LLM_TCK_{provider}_COMPATIBILITY",
            enabled ? "true" : "false"
        );
    }

    private static IResourceBuilder<TResource> ConfigureLlmTckResource<TResource>(
        IResourceBuilder<TResource> builder,
        string name
    )
        where TResource : IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("LLM_TCK_RESOURCE_NAME", name)
            .WithEnvironment("LLM_TCK_OPENAI_COMPATIBILITY", "true");
    }
}

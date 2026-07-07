using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ManagedCode.LlmTck.Aspire;

public static class LlmTckAspireExtensions
{
    public static IResourceBuilder<ProjectResource> AddLlmTck(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectPath
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        return builder
            .AddProject(name, projectPath)
            .WithEnvironment("LLM_TCK_RESOURCE_NAME", name)
            .WithEnvironment("LLM_TCK_OPENAI_COMPATIBILITY", "true");
    }

    public static IResourceBuilder<ProjectResource> WithOpenAICompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "OPENAI", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithAzureOpenAICompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "AZURE_OPENAI", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithFoundryCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "MICROSOFT_FOUNDRY", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithAnthropicCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "ANTHROPIC", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithGeminiCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "GEMINI", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithGroqCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "GROQ", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithMistralCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "MISTRAL", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithOllamaCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "OLLAMA", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithCohereCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "COHERE", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithBedrockCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "BEDROCK", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithOpenRouterCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "OPENROUTER", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithDeepSeekCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "DEEPSEEK", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithPerplexityCompatibility(
        this IResourceBuilder<ProjectResource> builder,
        bool enabled = true
    )
    {
        return WithCompatibility(builder, "PERPLEXITY", enabled);
    }

    public static IResourceBuilder<ProjectResource> WithEndpoint(
        this IResourceBuilder<ProjectResource> builder,
        string endpoint
    )
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

    public static IResourceBuilder<ProjectResource> WithApiKey(
        this IResourceBuilder<ProjectResource> builder,
        string apiKey
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return builder.WithEnvironment("LlmTck__RequiredBearerToken", apiKey);
    }

    private static IResourceBuilder<ProjectResource> WithCompatibility(
        IResourceBuilder<ProjectResource> builder,
        string provider,
        bool enabled
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return builder.WithEnvironment(
            $"LLM_TCK_{provider}_COMPATIBILITY",
            enabled ? "true" : "false"
        );
    }
}

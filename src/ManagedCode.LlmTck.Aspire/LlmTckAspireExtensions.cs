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
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("LLM_TCK_OPENAI_COMPATIBILITY", enabled ? "true" : "false");
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
}

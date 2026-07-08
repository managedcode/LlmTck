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

        return builder.AddResource(resource)
            .WithImage(LlmTckContainerImageTags.Image, LlmTckContainerImageTags.Tag)
            .WithImageRegistry(LlmTckContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: LlmTckResource.HttpPort, name: "http")
            .WithHttpHealthCheck("/");
    }

    public static EndpointReference GetHttpEndpoint(
        this IResourceBuilder<LlmTckResource> builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.GetEndpoint("http");
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

}

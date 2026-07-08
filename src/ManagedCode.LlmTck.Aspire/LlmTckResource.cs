using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ManagedCode.LlmTck.Aspire;

public sealed class LlmTckResource(string name, string command, string workingDirectory)
    : ExecutableResource(name, command, workingDirectory),
        IResourceWithServiceDiscovery
{
    public const string DefaultName = "llm-tck";

    public const string HttpEndpointName = "http";
}

public sealed class LlmTckContainerResource(string name)
    : ContainerResource(name),
        IResourceWithServiceDiscovery
{
    public const int HttpPort = 8080;
}

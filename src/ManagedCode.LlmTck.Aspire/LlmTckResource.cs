using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ManagedCode.LlmTck.Aspire;

public sealed class LlmTckResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    public const string DefaultName = "llm-tck";

    public const int HttpPort = 8080;
}

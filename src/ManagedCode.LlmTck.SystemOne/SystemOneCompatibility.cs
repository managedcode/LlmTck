using ManagedCode.LlmTck.Providers;
namespace ManagedCode.LlmTck.SystemOne;

public static class SystemOneCompatibility
{
    public const string ProviderId = LlmTckCompatibilityTags.SystemOne;
    public static LlmTckProviderProfile Profile { get; } = new()
    {
        Id = ProviderId,
        DisplayName = "SystemOne (Jev and Kev)",
        Protocol = LlmTckProtocolFamily.SystemOne,
        DefaultEndpointPath = "/systemone/v1/systemone",
        Capabilities = [LlmTckProviderCapability.Decisions],
        CompatibilityTags = [ProviderId],
        ApiContract = new()
        {
            DocumentationUrl = "https://api.typesafe.ai/openapi.json",
            DocumentationRetrievedOn = "2026-10-08",
            DocumentationVersion = "native decisions v1",
            Operations = [new() { Id = LlmTckProviderOperationIds.SystemOne.DecisionsCreate, Method = "POST", Path = "/systemone/v1/systemone", DocumentationUrl = "https://api.typesafe.ai/openapi.json", ImplementedByHosting = true, Capabilities = [LlmTckProviderCapability.Decisions] }]
        },
    };
}

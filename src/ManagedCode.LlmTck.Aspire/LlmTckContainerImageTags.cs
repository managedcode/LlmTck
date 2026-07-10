using System.Reflection;

namespace ManagedCode.LlmTck.Aspire;

public static class LlmTckContainerImageTags
{
    private const string _fallbackTag = "0.0.12";

    public const string Registry = "ghcr.io";

    public const string Image = "managedcode/llm-tck";

    public static string Tag { get; } = GetPackageVersion();

    private static string GetPackageVersion()
    {
        var version = typeof(LlmTckContainerImageTags)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return _fallbackTag;
        }

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex > 0 ? version[..metadataIndex] : version;
    }
}

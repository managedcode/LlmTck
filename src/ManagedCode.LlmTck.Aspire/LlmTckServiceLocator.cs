namespace ManagedCode.LlmTck.Aspire;

internal static class LlmTckServiceLocator
{
    private const string _serviceDirectoryName = "llm-tck-service";
    private const string _serviceAssemblyName = "ManagedCode.LlmTck.Service.dll";

    public static string GetServiceDirectory()
    {
        var assemblyDirectory = AppContext.BaseDirectory;
        var packageServiceDirectory = Path.Combine(assemblyDirectory, _serviceDirectoryName);
        if (ContainsServiceAssembly(packageServiceDirectory))
        {
            return packageServiceDirectory;
        }

        var sourceServiceDirectory = FindSourceServiceDirectory(assemblyDirectory);
        if (sourceServiceDirectory is not null)
        {
            return sourceServiceDirectory;
        }

        throw new InvalidOperationException(
            $"The packaged LLM TCK service executable was not found at '{packageServiceDirectory}'. "
                + "Restore the ManagedCode.LlmTck.Aspire package or use AddLlmTckContainer() for the container-backed resource."
        );
    }

    private static bool ContainsServiceAssembly(string directory)
    {
        return File.Exists(Path.Combine(directory, _serviceAssemblyName));
    }

    private static string? FindSourceServiceDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "ManagedCode.LlmTck.slnx");
            if (File.Exists(solutionPath))
            {
                foreach (var configuration in new[] { "Release", "Debug" })
                {
                    var candidate = Path.Combine(
                        current.FullName,
                        "samples",
                        "ManagedCode.LlmTck.Service",
                        "bin",
                        configuration,
                        "net10.0"
                    );
                    if (ContainsServiceAssembly(candidate))
                    {
                        return candidate;
                    }
                }
            }

            current = current.Parent;
        }

        return null;
    }
}

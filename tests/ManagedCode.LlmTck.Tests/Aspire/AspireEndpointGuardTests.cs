namespace ManagedCode.LlmTck.Tests.AspireIntegration;

public sealed class AspireEndpointGuardTests
{
    [Test]
    public async Task AspireWiring_DoesNotUseManualProviderEndpointConfigurationAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        string[] guardedFiles =
        [
            "src/ManagedCode.LlmTck.Aspire/LlmTckAspireExtensions.cs",
            "samples/ManagedCode.LlmTck.AppHost/AppHost.cs",
            "samples/ManagedCode.LlmTck.Service/Program.cs",
            "tests/ManagedCode.LlmTck.Tests/Aspire/AspireIntegrationTests.cs",
            "docs/Architecture.md",
            "docs/Testing/AcceptanceCriteriaTestInventory.md",
            "README.md",
        ];
        string[] forbiddenFragments =
        [
            ".WithEndpoint(",
            "127.0.0.1",
            "LlmTck__Endpoint",
            "LLM_TCK_ENDPOINT",
        ];

        foreach (var relativePath in guardedFiles)
        {
            var path = Path.Combine(repositoryRoot, relativePath);
            var text = File.ReadAllText(path);

            foreach (var forbiddenFragment in forbiddenFragments)
            {
                var searchIndex = text.IndexOf(forbiddenFragment, StringComparison.Ordinal);
                while (searchIndex >= 0)
                {
                    violations.Add(
                        $"{relativePath}:{GetLineNumber(text, searchIndex)} contains {forbiddenFragment}; use the Aspire resource endpoint instead."
                    );
                    searchIndex = text.IndexOf(
                        forbiddenFragment,
                        searchIndex + forbiddenFragment.Length,
                        StringComparison.Ordinal
                    );
                }
            }
        }

        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        await Assert.That(readme).Contains(".WithReference(llmTck)");
        await Assert.That(readme).Contains("llmTck.GetHttpEndpoint()");
        await Assert.That(string.Join(Environment.NewLine, violations)).IsEqualTo(string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ManagedCode.LlmTck.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find ManagedCode.LlmTck.slnx.");
    }

    private static int GetLineNumber(string sourceText, int characterIndex)
    {
        var lineNumber = 1;

        for (var i = 0; i < characterIndex; i++)
        {
            if (sourceText[i] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }
}

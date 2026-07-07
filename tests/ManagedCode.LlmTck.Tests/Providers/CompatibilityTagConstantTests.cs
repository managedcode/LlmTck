using System.Reflection;
using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class CompatibilityTagConstantTests
{
    [Test]
    public async Task CompatibilityTagValues_AreNotTypedAsInlineStringLiteralsAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tagValues = typeof(LlmTckCompatibilityTags)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Select(field => new
            {
                field.Name,
                Value = field.GetRawConstantValue() as string,
            })
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Value))
            .ToArray();
        var violations = new List<string>();

        foreach (var filePath in EnumerateCSharpSourceFiles(repositoryRoot))
        {
            var sourceText = File.ReadAllText(filePath);
            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);

            foreach (var tag in tagValues)
            {
                var literal = $"\"{tag.Value}\"";
                var searchIndex = sourceText.IndexOf(literal, StringComparison.Ordinal);

                while (searchIndex >= 0)
                {
                    violations.Add(
                        $"{relativePath}:{GetLineNumber(sourceText, searchIndex)} uses {literal}; use {nameof(LlmTckCompatibilityTags)}.{tag.Name}."
                    );
                    searchIndex = sourceText.IndexOf(
                        literal,
                        searchIndex + literal.Length,
                        StringComparison.Ordinal
                    );
                }
            }
        }

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

    private static IEnumerable<string> EnumerateCSharpSourceFiles(string repositoryRoot)
    {
        string[] sourceDirectories =
        [
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "tests"),
            Path.Combine(repositoryRoot, "samples"),
        ];

        return sourceDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
                !path.EndsWith(
                    Path.Combine("Providers", "LlmTckCompatibilityTags.cs"),
                    StringComparison.Ordinal
                )
            )
            .Where(path =>
                !path.EndsWith(
                    Path.Combine("Providers", "CompatibilityTagConstantTests.cs"),
                    StringComparison.Ordinal
                )
            );
    }

    private static bool IsBuildOutput(string path)
    {
        var normalizedPath = path.Replace(Path.DirectorySeparatorChar, '/');

        return normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            || normalizedPath.Contains("/obj/", StringComparison.Ordinal);
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

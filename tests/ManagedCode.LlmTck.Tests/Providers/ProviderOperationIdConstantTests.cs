using System.Reflection;
using ManagedCode.LlmTck.Providers;

namespace ManagedCode.LlmTck.Tests.Providers;

public sealed class ProviderOperationIdConstantTests
{
    [Test]
    public async Task ProviderOperationIds_AreNotTypedAsInlineStringLiteralsAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var operationIds = EnumerateOperationIdConstants()
            .Where(operationId => !string.IsNullOrWhiteSpace(operationId.Value))
            .DistinctBy(operationId => operationId.Value)
            .ToArray();
        var violations = new List<string>();

        foreach (var filePath in EnumerateCSharpSourceFiles(repositoryRoot))
        {
            var sourceText = File.ReadAllText(filePath);
            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);

            foreach (var operationId in operationIds)
            {
                var literal = $"\"{operationId.Value}\"";
                var searchIndex = sourceText.IndexOf(literal, StringComparison.Ordinal);

                while (searchIndex >= 0)
                {
                    violations.Add(
                        $"{relativePath}:{GetLineNumber(sourceText, searchIndex)} uses {literal}; use {operationId.Name}."
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

    private static IEnumerable<(string Name, string? Value)> EnumerateOperationIdConstants()
    {
        return EnumerateConstantStringFields(
            typeof(LlmTckProviderOperationIds),
            nameof(LlmTckProviderOperationIds)
        );
    }

    private static IEnumerable<(string Name, string? Value)> EnumerateConstantStringFields(
        Type type,
        string qualifiedName
    )
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            {
                yield return (
                    $"{qualifiedName}.{field.Name}",
                    field.GetRawConstantValue() as string
                );
            }
        }

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            foreach (var field in EnumerateConstantStringFields(
                nestedType,
                $"{qualifiedName}.{nestedType.Name}"
            ))
            {
                yield return field;
            }
        }
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
                    Path.Combine("Providers", "LlmTckProviderOperationIds.cs"),
                    StringComparison.Ordinal
                )
            )
            .Where(path =>
                !path.EndsWith(
                    Path.Combine("Providers", "ProviderOperationIdConstantTests.cs"),
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

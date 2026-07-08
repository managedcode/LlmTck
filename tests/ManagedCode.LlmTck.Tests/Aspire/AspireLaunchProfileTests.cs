using System.Text.Json;

namespace ManagedCode.LlmTck.Tests.AspireIntegration;

public sealed class AspireLaunchProfileTests
{
    [Test]
    public async Task SampleAppHost_HttpLaunchProfileAllowsUnsecuredTransportAsync()
    {
        var launchSettingsPath = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "ManagedCode.LlmTck.AppHost",
            "Properties",
            "launchSettings.json"
        );
        await using var stream = File.OpenRead(launchSettingsPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var httpProfile = document.RootElement
            .GetProperty("profiles")
            .GetProperty("http");
        var applicationUrl = httpProfile.GetProperty("applicationUrl").GetString();
        var environmentVariables = httpProfile.GetProperty("environmentVariables");

        if (applicationUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) is true)
        {
            await Assert.That(
                    environmentVariables
                        .GetProperty("ASPIRE_ALLOW_UNSECURED_TRANSPORT")
                        .GetString()
                )
                .IsEqualTo("true");
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "ManagedCode.LlmTck.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the ManagedCode.LlmTck repository root.");
    }
}

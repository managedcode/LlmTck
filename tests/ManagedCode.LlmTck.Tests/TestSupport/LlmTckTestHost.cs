using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ManagedCode.LlmTck.Tests.TestSupport;

internal static class LlmTckTestHost
{
    public static async Task<IHost> StartAsync(
        Action<LlmTckConfigurationBuilder>? configure = null,
        CancellationToken cancellationToken = default
    )
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddLlmTck(configure);
                    })
                    .Configure(application =>
                    {
                        application.UseRouting();
                        application.UseEndpoints(endpoints => endpoints.MapLlmTck());
                    }))
            .Build();

        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }
}

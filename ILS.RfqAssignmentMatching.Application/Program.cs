using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using ILS.RfqAssignmentMatching.Application.Batching;
using ILS.RfqAssignmentMatching.Application.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace ILS.RfqAssignmentMatching.Application;

/// <summary>
/// Console host entry point: loads configuration, registers DI, starts the host for console lifetime, runs
/// <see cref="RfqMatchingBatchProcessor.RunAsync"/> with <see cref="IHostApplicationLifetime.ApplicationStopping"/>,
/// then stops the host.
/// </summary>
[ExcludeFromCodeCoverage]
internal class Program
{
    /// <summary>
    /// Application entry: builds the host, starts console lifetime (Ctrl+C → <see cref="IHostApplicationLifetime.ApplicationStopping"/>),
    /// runs one batch, then stops the host.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    [SupportedOSPlatform("windows")]
    private static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        await host.StartAsync().ConfigureAwait(false);
        try
        {
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            await host.Services.GetRequiredService<RfqMatchingBatchProcessor>()
                .RunAsync(lifetime.ApplicationStopping)
                .ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Configures JSON configuration (appsettings + external path), DI, and logging.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The host builder.</returns>
    [SupportedOSPlatform("windows")]
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        // Host default when DOTNET_ENVIRONMENT / ASPNETCORE_ENVIRONMENT are unset is Production.
        // Debug builds default to Development so local runs load appsettings.Development.json without extra env setup.
        // Release deployments must set DOTNET_ENVIRONMENT (e.g. Production, QA, Integration) on the server or task.
        var hostBuilder = Host.CreateDefaultBuilder(args);
#if DEBUG
        hostBuilder = hostBuilder.UseEnvironment(
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development");
#endif
        return hostBuilder
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                const string externalApplicationConfigurationRootPath = @"c:\devops\applicationConfiguration";

                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: false, reloadOnChange: true);

                config.AddJsonFile(
                    Path.Combine(externalApplicationConfigurationRootPath, $@"{env.EnvironmentName}\configuration.json"),
                    optional: false,
                    reloadOnChange: true);
            })
            .ConfigureServices(services =>
            {
                services
                    .RegisterRfqManagementHttpApiClient()
                    .RegisterBatchProcessor();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog(new EventLogSettings
                {
                    SourceName = "ILS.RfqAssignmentMatching",
                    LogName = "Application"
                });
            })
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = true;
            });
    }
}

using System.Diagnostics.CodeAnalysis;
using ILS.RfqAssignmentMatching.Application.Authentication;
using ILS.RfqAssignmentMatching.Application.Batching;
using ILS.RfqAssignmentMatching.Application.Configuration;
using ILS.RfqAssignmentMatching.Application.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ILS.RfqAssignmentMatching.Application.Extensions;

/// <summary>Registers the OAuth token provider, the named RfqManagement <see cref="HttpClient"/>, and the batch processor.</summary>
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IClientCredentialsTokenProvider"/>, the named <see cref="HttpClient"/> for
    /// ILS.RfqManagement.Service (base address from <c>ServiceDependencies</c>), and <see cref="IRfqManagementApiClient"/>.
    /// </summary>
    /// <param name="services">The host <see cref="IServiceCollection"/>.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection RegisterRfqManagementHttpApiClient(this IServiceCollection services)
    {
        services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken);
        services.AddSingleton<IClientCredentialsTokenProvider, ClientCredentialsTokenProvider>();

        services.AddHttpClient(RfqManagementApiClient.HttpClientNameRfqManagement, (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var url = ServiceDependency.GetRfqManagementServiceUrlRoot(config);
            if (!string.IsNullOrWhiteSpace(url))
                client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        });

        services.AddSingleton<IRfqManagementApiClient, RfqManagementApiClient>();

        return services;
    }

    /// <summary>Registers <see cref="RfqMatchingBatchProcessor"/>.</summary>
    /// <param name="services">The host <see cref="IServiceCollection"/>.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection RegisterBatchProcessor(this IServiceCollection services)
    {
        return services.AddSingleton<RfqMatchingBatchProcessor>();
    }
}

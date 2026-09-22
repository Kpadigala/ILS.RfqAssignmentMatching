using Microsoft.Extensions.Configuration;

namespace ILS.RfqAssignmentMatching.Application.Configuration;

/// <summary>Maps a named HTTP dependency from <c>ServiceDependencies</c> in configuration.</summary>
public class ServiceDependency
{
    private const string RfqManagementDependencyName = "RfqManagement";

    /// <summary>Dependency name from configuration (e.g. <c>RfqManagement</c>).</summary>
    public string Name { get; set; }

    /// <summary>Full base URL for the dependency (e.g. <c>http://localhost:7220</c>).</summary>
    public string UrlRoot { get; set; }

    /// <summary>Resolves the RfqManagement API base URL from the <c>ServiceDependencies</c> entry named <c>RfqManagement</c>.</summary>
    public static string GetRfqManagementServiceUrlRoot(IConfiguration configuration) =>
        GetServiceUrlRoot(configuration, RfqManagementDependencyName);

    /// <summary>Reads <c>ServiceDependencies</c>: returns the entry's <see cref="UrlRoot"/> when set.</summary>
    private static string GetServiceUrlRoot(IConfiguration configuration, string dependencyName)
    {
        var deps = configuration.GetSection("ServiceDependencies").Get<List<ServiceDependency>>();
        if (deps == null || deps.Count == 0)
            return null;

        var match = deps.FirstOrDefault(d => string.Equals(d?.Name, dependencyName, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(match?.UrlRoot) ? match.UrlRoot.Trim() : null;
    }
}

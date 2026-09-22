using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ILS.RfqAssignmentMatching.Application.Authentication;
using ILS.RfqAssignmentMatching.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ILS.RfqAssignmentMatching.Application.Http;

/// <summary>
/// HTTP façade for ILS.RfqManagement.Service's pending-match endpoints (named <see cref="HttpClient"/> from <see cref="IHttpClientFactory"/>).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RfqManagementApiClient"/> class.
/// </remarks>
/// <param name="httpClientFactory">Factory for the named RfqManagement HTTP client.</param>
/// <param name="configuration">Used to resolve the base URL and optional OAuth.</param>
/// <param name="tokenProvider">Bearer token for authenticated outbound calls.</param>
/// <param name="logger">Logger.</param>
public class RfqManagementApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IClientCredentialsTokenProvider tokenProvider,
    ILogger<RfqManagementApiClient> logger) : IRfqManagementApiClient
{
    /// <summary>Named <see cref="HttpClient"/> for ILS.RfqManagement.Service.</summary>
    public const string HttpClientNameRfqManagement = "RfqAssignmentMatching.RfqManagement";

    private const string PendingRfqMatchesRelativePath = "api/rfqmanagement/pendingRfqMatches";
    private const string MatchNewRfqToAssignmentRulesRelativePath = "api/rfqmanagement/matchNewRfqToAssignmentRules";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <inheritdoc />
    public async Task<IEnumerable<PendingRfqMatch>> GetPendingRfqMatchesAsync(CancellationToken cancellationToken = default)
    {
        var client = GetConfiguredClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, PendingRfqMatchesRelativePath);
        await AddBearerIfConfiguredAsync(request, cancellationToken).ConfigureAwait(false);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "GET", PendingRfqMatchesRelativePath, cancellationToken).ConfigureAwait(false);

        var matches = await response.Content
            .ReadFromJsonAsync<IEnumerable<PendingRfqMatch>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return matches ?? [];
    }

    /// <inheritdoc />
    public async Task MatchNewRfqToAssignmentRulesAsync(PendingRfqMatch match, CancellationToken cancellationToken = default)
    {
        var client = GetConfiguredClient();

        var body = new
        {
            RfqId = match.RfqId,
            SupplierCompanyId = match.SupplierCompanyId,
            BuyerCompanyId = match.BuyerCompanyId,
            PartNumbers = SplitPartNumbers(match.PartNumbers),
            RfqTypeCd = match.RfqTypeCd
        };

        using var content = JsonContent.Create(body, options: JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, MatchNewRfqToAssignmentRulesRelativePath) { Content = content };
        await AddBearerIfConfiguredAsync(request, cancellationToken).ConfigureAwait(false);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "POST", MatchNewRfqToAssignmentRulesRelativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearPendingRfqMatchAsync(string rfqId, CancellationToken cancellationToken = default)
    {
        var client = GetConfiguredClient();
        var relativePath = $"api/rfqmanagement/pendingRfqMatches/{Uri.EscapeDataString(rfqId)}/clear";

        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        await AddBearerIfConfiguredAsync(request, cancellationToken).ConfigureAwait(false);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "POST", relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// When <c>Auth0Authentication</c> is present, attaches <c>Bearer</c> from the token provider; logs a warning if no token.
    /// </summary>
    private async Task AddBearerIfConfiguredAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!configuration.GetSection("Auth0Authentication").Exists())
            return;

        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            logger.LogWarning("RfqManagement API request: no access token; request sent without Authorization.");
    }

    /// <summary>Throws with a logged, truncated response body when the response is not a success status code.</summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, string method, string relativePath, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var snippet = body.Length > 500 ? body[..500] + "…" : body;
        var httpEx = new HttpRequestException(
            $"RfqManagement API {method} {relativePath} failed with status {(int)response.StatusCode}: {snippet}");
        logger.LogError(httpEx, "RfqManagement API {Method} {Path} failed with {StatusCode}: {Body}", method, relativePath, (int)response.StatusCode, snippet);
        throw httpEx;
    }

    /// <summary>Resolves the named client, throwing when its base address is not configured.</summary>
    private HttpClient GetConfiguredClient()
    {
        var client = httpClientFactory.CreateClient(HttpClientNameRfqManagement);
        if (client.BaseAddress == null)
        {
            var url = ServiceDependency.GetRfqManagementServiceUrlRoot(configuration);
            throw new InvalidOperationException(
                url == null
                    ? "ServiceDependencies has no 'RfqManagement' entry configured."
                    : $"RfqManagement base URL '{url}' could not be parsed as a URI.");
        }

        return client;
    }

    private static string[] SplitPartNumbers(string partNumbers) =>
        string.IsNullOrWhiteSpace(partNumbers)
            ? []
            : partNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ILS.RfqAssignmentMatching.Application.Authentication;

/// <summary>
/// Fetches an access token using <c>Auth0Authentication</c> (<c>TokenUrl</c>, <c>ClientId</c>, <c>ClientSecret</c>, <c>GrantType</c>, <c>Audience</c>),
/// matching the pattern used by ILS.AutoQuote.Application for authenticated outbound calls.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ClientCredentialsTokenProvider"/> class.
/// </remarks>
/// <param name="configuration">Application configuration (<c>Auth0Authentication</c> section).</param>
/// <param name="logger">Logger.</param>
/// <param name="httpClientFactory">Factory for the OAuth token HTTP client.</param>
public class ClientCredentialsTokenProvider(
    IConfiguration configuration,
    ILogger<ClientCredentialsTokenProvider> logger,
    IHttpClientFactory httpClientFactory) : IClientCredentialsTokenProvider
{
    /// <summary>Named <see cref="HttpClient"/> used only for the OAuth token POST (resolved via <see cref="IHttpClientFactory"/>).</summary>
    public const string HttpClientNameOAuthToken = "RfqAssignmentMatching.OAuthToken";

    /// <summary>
    /// Requests an OAuth access token from <c>Auth0Authentication:TokenUrl</c> using client credentials. Returns
    /// <see langword="null"/> when the configuration section is missing, required fields are absent, the HTTP call fails, or the
    /// JSON response has no <c>access_token</c> property.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the token HTTP request.</param>
    /// <returns>Bearer access token string, or <see langword="null"/> on failure or misconfiguration.</returns>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var authSection = configuration.GetSection("Auth0Authentication");
        if (!authSection.Exists())
            return null;

        var clientId = authSection["ClientId"];
        var clientSecret = authSection["ClientSecret"];
        var tokenUrl = authSection["TokenUrl"];
        var audience = authSection["Audience"];
        var grantType = authSection["GrantType"];

        if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            logger.LogWarning("Auth0Authentication section exists but TokenUrl, ClientId, or ClientSecret is missing.");
            return null;
        }

        var dict = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = string.IsNullOrWhiteSpace(grantType) ? "client_credentials" : grantType,
            ["audience"] = audience ?? string.Empty
        };

        using var content = new FormUrlEncodedContent(dict);
        using var client = httpClientFactory.CreateClient(HttpClientNameOAuthToken);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(tokenUrl, content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth token request to {TokenUrl} failed.", tokenUrl);
            return null;
        }

        var respContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OAuth token request failed with {StatusCode}: {Body}",
                (int)response.StatusCode,
                respContent.Length > 500 ? respContent[..500] + "…" : respContent);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(respContent);
            return doc.RootElement.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() : null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "OAuth token response was not valid JSON.");
            return null;
        }
    }
}

namespace ILS.RfqAssignmentMatching.Application.Authentication;

/// <summary>Fetches an Auth0 client-credentials access token for authenticated outbound calls.</summary>
public interface IClientCredentialsTokenProvider
{
    /// <summary>Returns <c>null</c> when <c>Auth0Authentication</c> is missing, the token request fails, or the response has no <c>access_token</c>.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

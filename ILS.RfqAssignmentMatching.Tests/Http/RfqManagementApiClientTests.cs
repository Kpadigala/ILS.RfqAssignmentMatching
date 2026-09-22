using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ILS.RfqAssignmentMatching.Application;
using ILS.RfqAssignmentMatching.Application.Authentication;
using ILS.RfqAssignmentMatching.Application.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace ILS.RfqAssignmentMatching.Tests.Http;

public class RfqManagementApiClientTests
{
    private static IConfiguration ServiceDependencyConfig(string urlRoot = "https://rest.example") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ServiceDependencies:0:Name"] = "RfqManagement",
                ["ServiceDependencies:0:UrlRoot"] = urlRoot
            })
            .Build();

    private static (RfqManagementApiClient Sut, Mock<HttpMessageHandler> Handler) CreateSut(
        HttpResponseMessage response, IConfiguration config = null, Mock<IClientCredentialsTokenProvider> tokenProvider = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://rest.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RfqManagementApiClient.HttpClientNameRfqManagement)).Returns(httpClient);

        var sut = new RfqManagementApiClient(
            factory.Object,
            config ?? ServiceDependencyConfig(),
            (tokenProvider ?? new Mock<IClientCredentialsTokenProvider>()).Object,
            NullLogger<RfqManagementApiClient>.Instance);

        return (sut, handler);
    }

    [Fact]
    public async Task GetPendingRfqMatchesAsync_WhenSuccess_ReturnsDeserializedMatches()
    {
        var matches = new[]
        {
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024", BuyerCompanyId = "9001", RfqTypeCd = "P", PartNumbers = "A100" }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(matches, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json")
        };
        var (sut, _) = CreateSut(response);

        var actual = (await sut.GetPendingRfqMatchesAsync()).ToList();

        actual.Count.ShouldBe(1);
        actual[0].RfqId.ShouldBe("rfq-1");
    }

    [Fact]
    public async Task GetPendingRfqMatchesAsync_WhenFailureStatus_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom", Encoding.UTF8, "text/plain")
        };
        var (sut, _) = CreateSut(response);

        await Should.ThrowAsync<HttpRequestException>(() => sut.GetPendingRfqMatchesAsync());
    }

    [Fact]
    public async Task GetPendingRfqMatchesAsync_WhenBaseAddressNotConfigured_Throws()
    {
        var handler = new Mock<HttpMessageHandler>();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RfqManagementApiClient.HttpClientNameRfqManagement)).Returns(new HttpClient(handler.Object));

        var sut = new RfqManagementApiClient(
            factory.Object,
            new ConfigurationBuilder().Build(),
            new Mock<IClientCredentialsTokenProvider>().Object,
            NullLogger<RfqManagementApiClient>.Instance);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetPendingRfqMatchesAsync());
    }

    [Fact]
    public async Task MatchNewRfqToAssignmentRulesAsync_WhenSuccess_SendsExpectedRequest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("true", Encoding.UTF8, "application/json") };
        var handler = new Mock<HttpMessageHandler>();
        HttpRequestMessage capturedRequest = null;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://rest.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RfqManagementApiClient.HttpClientNameRfqManagement)).Returns(httpClient);

        var sut = new RfqManagementApiClient(
            factory.Object,
            ServiceDependencyConfig(),
            new Mock<IClientCredentialsTokenProvider>().Object,
            NullLogger<RfqManagementApiClient>.Instance);

        await sut.MatchNewRfqToAssignmentRulesAsync(new PendingRfqMatch
        {
            RfqId = "rfq-1",
            SupplierCompanyId = "5024",
            BuyerCompanyId = "9001",
            RfqTypeCd = "P",
            PartNumbers = "A100,A200"
        });

        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri.ShouldBe(new Uri("https://rest.example/api/rfqmanagement/matchNewRfqToAssignmentRules"));
    }

    [Fact]
    public async Task MatchNewRfqToAssignmentRulesAsync_WhenFailureStatus_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
        };
        var (sut, _) = CreateSut(response);

        await Should.ThrowAsync<HttpRequestException>(() =>
            sut.MatchNewRfqToAssignmentRulesAsync(new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024", BuyerCompanyId = "9001" }));
    }

    [Fact]
    public async Task ClearPendingRfqMatchAsync_WhenSuccess_SendsExpectedRequest()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("true", Encoding.UTF8, "application/json") };
        var handler = new Mock<HttpMessageHandler>();
        HttpRequestMessage capturedRequest = null;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://rest.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RfqManagementApiClient.HttpClientNameRfqManagement)).Returns(httpClient);

        var sut = new RfqManagementApiClient(
            factory.Object,
            ServiceDependencyConfig(),
            new Mock<IClientCredentialsTokenProvider>().Object,
            NullLogger<RfqManagementApiClient>.Instance);

        await sut.ClearPendingRfqMatchAsync("rfq-1");

        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri.ShouldBe(new Uri("https://rest.example/api/rfqmanagement/pendingRfqMatches/rfq-1/clear"));
    }

    [Fact]
    public async Task ClearPendingRfqMatchAsync_WhenFailureStatus_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom", Encoding.UTF8, "text/plain")
        };
        var (sut, _) = CreateSut(response);

        await Should.ThrowAsync<HttpRequestException>(() => sut.ClearPendingRfqMatchAsync("rfq-1"));
    }

    [Fact]
    public async Task MatchNewRfqToAssignmentRulesAsync_WhenAuth0Configured_AttachesBearerToken()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("true", Encoding.UTF8, "application/json") };
        var handler = new Mock<HttpMessageHandler>();
        HttpRequestMessage capturedRequest = null;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://rest.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RfqManagementApiClient.HttpClientNameRfqManagement)).Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ServiceDependencies:0:Name"] = "RfqManagement",
                ["ServiceDependencies:0:UrlRoot"] = "https://rest.example",
                ["Auth0Authentication:TokenUrl"] = "https://auth.example/oauth/token"
            })
            .Build();

        var tokenProvider = new Mock<IClientCredentialsTokenProvider>();
        tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok-abc");

        var sut = new RfqManagementApiClient(factory.Object, config, tokenProvider.Object, NullLogger<RfqManagementApiClient>.Instance);

        await sut.MatchNewRfqToAssignmentRulesAsync(new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024", BuyerCompanyId = "9001" });

        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("tok-abc");
    }
}

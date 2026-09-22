using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ILS.RfqAssignmentMatching.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace ILS.RfqAssignmentMatching.Tests.Authentication;

public class ClientCredentialsTokenProviderTests
{
    private static IConfiguration AuthConfig(Dictionary<string, string> extra = null)
    {
        var data = new Dictionary<string, string>
        {
            ["Auth0Authentication:TokenUrl"] = "https://auth.example/oauth/token",
            ["Auth0Authentication:ClientId"] = "cid",
            ["Auth0Authentication:ClientSecret"] = "sec"
        };
        if (extra != null)
        {
            foreach (var kv in extra)
                data[kv.Key] = kv.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenAuthSectionMissing_ReturnsNull()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var factory = new Mock<IHttpClientFactory>();
        var sut = new ClientCredentialsTokenProvider(config, NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
        factory.Verify(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken), Times.Never);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenUrlMissing_ReturnsNullAndLogs()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Auth0Authentication:ClientId"] = "x",
                ["Auth0Authentication:ClientSecret"] = "y"
            })
            .Build();
        var factory = new Mock<IHttpClientFactory>();
        var sut = new ClientCredentialsTokenProvider(config, NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenSuccess_ReturnsAccessToken()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["access_token"] = "tok-abc" }),
                    Encoding.UTF8,
                    "application/json")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBe("tok-abc");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenHttpFails_LogsTruncatedBodyWhenLong()
    {
        var longBody = new string('e', 600);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(longBody, Encoding.UTF8, "text/plain")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenHttpFails_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("error body", Encoding.UTF8, "text/plain")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenSendThrows_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network"));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenJsonMissingAccessToken_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenResponseNotJson_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "text/plain")
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ClientCredentialsTokenProvider.HttpClientNameOAuthToken))
            .Returns(new HttpClient(handler.Object));

        var sut = new ClientCredentialsTokenProvider(AuthConfig(), NullLogger<ClientCredentialsTokenProvider>.Instance, factory.Object);

        (await sut.GetAccessTokenAsync()).ShouldBeNull();
    }
}

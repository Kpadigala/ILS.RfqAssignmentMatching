using ILS.RfqAssignmentMatching.Application;
using ILS.RfqAssignmentMatching.Application.Batching;
using ILS.RfqAssignmentMatching.Application.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ILS.RfqAssignmentMatching.Tests.Batching;

public class RfqMatchingBatchProcessorTests
{
    private static IHostEnvironment Host()
    {
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns("Development");
        return host.Object;
    }

    [Fact]
    public async Task RunAsync_WhenNoPendingMatches_DoesNothing()
    {
        var apiClient = new Mock<IRfqManagementApiClient>();
        apiClient.Setup(c => c.GetPendingRfqMatchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = new RfqMatchingBatchProcessor(apiClient.Object, NullLogger<RfqMatchingBatchProcessor>.Instance, Host());

        await sut.RunAsync();

        apiClient.Verify(c => c.MatchNewRfqToAssignmentRulesAsync(It.IsAny<PendingRfqMatch>(), It.IsAny<CancellationToken>()), Times.Never);
        apiClient.Verify(c => c.ClearPendingRfqMatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenGetPendingRfqMatchesThrows_AbortsWithoutThrowing()
    {
        var apiClient = new Mock<IRfqManagementApiClient>();
        apiClient.Setup(c => c.GetPendingRfqMatchesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        var sut = new RfqMatchingBatchProcessor(apiClient.Object, NullLogger<RfqMatchingBatchProcessor>.Instance, Host());

        await sut.RunAsync();

        apiClient.Verify(c => c.MatchNewRfqToAssignmentRulesAsync(It.IsAny<PendingRfqMatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenAllSuppliersForRfqSucceed_ClearsThatRfq()
    {
        var matches = new[]
        {
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024" },
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5030" }
        };
        var apiClient = new Mock<IRfqManagementApiClient>();
        apiClient.Setup(c => c.GetPendingRfqMatchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        var sut = new RfqMatchingBatchProcessor(apiClient.Object, NullLogger<RfqMatchingBatchProcessor>.Instance, Host());

        await sut.RunAsync();

        apiClient.Verify(c => c.MatchNewRfqToAssignmentRulesAsync(It.Is<PendingRfqMatch>(m => m.SupplierCompanyId == "5024"), It.IsAny<CancellationToken>()), Times.Once);
        apiClient.Verify(c => c.MatchNewRfqToAssignmentRulesAsync(It.Is<PendingRfqMatch>(m => m.SupplierCompanyId == "5030"), It.IsAny<CancellationToken>()), Times.Once);
        apiClient.Verify(c => c.ClearPendingRfqMatchAsync("rfq-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenOneSupplierForRfqFails_DoesNotClearThatRfqButStillProcessesOthers()
    {
        var matches = new[]
        {
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024" },
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5030" },
            new PendingRfqMatch { RfqId = "rfq-2", SupplierCompanyId = "6000" }
        };
        var apiClient = new Mock<IRfqManagementApiClient>();
        apiClient.Setup(c => c.GetPendingRfqMatchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        apiClient
            .Setup(c => c.MatchNewRfqToAssignmentRulesAsync(It.Is<PendingRfqMatch>(m => m.SupplierCompanyId == "5030"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("failed"));
        var sut = new RfqMatchingBatchProcessor(apiClient.Object, NullLogger<RfqMatchingBatchProcessor>.Instance, Host());

        await sut.RunAsync();

        apiClient.Verify(c => c.ClearPendingRfqMatchAsync("rfq-1", It.IsAny<CancellationToken>()), Times.Never);
        apiClient.Verify(c => c.ClearPendingRfqMatchAsync("rfq-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenClearPendingRfqMatchThrows_DoesNotThrowAndContinuesBatch()
    {
        var matches = new[]
        {
            new PendingRfqMatch { RfqId = "rfq-1", SupplierCompanyId = "5024" },
            new PendingRfqMatch { RfqId = "rfq-2", SupplierCompanyId = "6000" }
        };
        var apiClient = new Mock<IRfqManagementApiClient>();
        apiClient.Setup(c => c.GetPendingRfqMatchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        apiClient
            .Setup(c => c.ClearPendingRfqMatchAsync("rfq-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("failed"));
        var sut = new RfqMatchingBatchProcessor(apiClient.Object, NullLogger<RfqMatchingBatchProcessor>.Instance, Host());

        await sut.RunAsync();

        apiClient.Verify(c => c.ClearPendingRfqMatchAsync("rfq-2", It.IsAny<CancellationToken>()), Times.Once);
    }
}

using ILS.RfqAssignmentMatching.Application.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ILS.RfqAssignmentMatching.Application.Batching;

/// <summary>
/// Polls ILS.RfqManagement.Service for RFQs still awaiting assignment-rule matching, matches each RFQ/supplier
/// pair, and clears the RFQ's pending flag once every one of its suppliers has matched successfully. Per the
/// RFQ Assignment Architecture decision memo (2026-09-09): a flag-and-poll batch, run to completion on a
/// schedule (Windows Task Scheduler), not a long-running loop. A pair that fails is left pending and retried
/// on the next scheduled run, so one bad RFQ never blocks or aborts the rest of the batch.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RfqMatchingBatchProcessor"/> class.
/// </remarks>
/// <param name="apiClient">Outbound calls to ILS.RfqManagement.Service.</param>
/// <param name="logger">Logger.</param>
/// <param name="host">Hosting environment (used for the environment name in logs).</param>
public class RfqMatchingBatchProcessor(
    IRfqManagementApiClient apiClient,
    ILogger<RfqMatchingBatchProcessor> logger,
    IHostEnvironment host)
{
    /// <summary>Runs one batch pass: fetch pending matches, match each pair, clear fully-matched RFQs.</summary>
    /// <param name="cancellationToken">Cancellation token, tied to host shutdown.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting RFQ Assignment Matching batch [{Environment}].", host.EnvironmentName);

        IEnumerable<PendingRfqMatch> pendingMatches;
        try
        {
            pendingMatches = await apiClient.GetPendingRfqMatchesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve pending RFQ matches; batch aborted.");
            return;
        }

        var rfqGroups = pendingMatches.GroupBy(match => match.RfqId).ToList();
        if (rfqGroups.Count == 0)
        {
            logger.LogInformation("No pending RFQ matches; batch finished.");
            return;
        }

        logger.LogInformation("Processing {Count} pending RFQs.", rfqGroups.Count);

        foreach (var rfqGroup in rfqGroups)
            await ProcessRfqGroupAsync(rfqGroup.Key, rfqGroup, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("RFQ Assignment Matching batch finished.");
    }

    /// <summary>Matches every supplier pair for one RFQ; clears the RFQ's pending flag only if all of them succeeded.</summary>
    private async Task ProcessRfqGroupAsync(string rfqId, IEnumerable<PendingRfqMatch> matches, CancellationToken cancellationToken)
    {
        var allSucceeded = true;

        foreach (var match in matches)
        {
            try
            {
                await apiClient.MatchNewRfqToAssignmentRulesAsync(match, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                logger.LogError(ex, "Failed matching RfqId {RfqId} for SupplierCompanyId {SupplierCompanyId}; it will remain pending for the next run.", rfqId, match.SupplierCompanyId);
            }
        }

        if (!allSucceeded)
            return;

        try
        {
            await apiClient.ClearPendingRfqMatchAsync(rfqId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Matched RfqId {RfqId} but failed clearing its pending flag; it will be re-matched on the next run.", rfqId);
        }
    }
}

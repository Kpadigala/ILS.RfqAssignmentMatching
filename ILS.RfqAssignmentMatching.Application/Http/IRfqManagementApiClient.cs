namespace ILS.RfqAssignmentMatching.Application.Http;

/// <summary>Outbound calls to ILS.RfqManagement.Service's pending-match endpoints.</summary>
public interface IRfqManagementApiClient
{
    /// <summary>GETs every RFQ/supplier pair still awaiting assignment-rule matching.</summary>
    Task<IEnumerable<PendingRfqMatch>> GetPendingRfqMatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>POSTs one RFQ/supplier pair to be matched against active assignment rules.</summary>
    Task MatchNewRfqToAssignmentRulesAsync(PendingRfqMatch match, CancellationToken cancellationToken = default);

    /// <summary>POSTs that an RFQ is done matching, once every supplier pair for it has succeeded.</summary>
    Task ClearPendingRfqMatchAsync(string rfqId, CancellationToken cancellationToken = default);
}

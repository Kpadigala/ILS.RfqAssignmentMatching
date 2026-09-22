namespace ILS.RfqAssignmentMatching.Application;

/// <summary>
/// One RFQ/supplier pair still awaiting assignment-rule matching, as returned by
/// <c>GET api/rfqmanagement/pendingRfqMatches</c> on ILS.RfqManagement.Service.
/// </summary>
public class PendingRfqMatch
{
    /// <summary>Gets or sets the RFQ identifier.</summary>
    public string RfqId { get; set; }

    /// <summary>Gets or sets the RFQ's recipient supplier company (Receiving ID).</summary>
    public string SupplierCompanyId { get; set; }

    /// <summary>Gets or sets the buyer company that created the RFQ.</summary>
    public string BuyerCompanyId { get; set; }

    /// <summary>Gets or sets the RFQ type code.</summary>
    public string RfqTypeCd { get; set; }

    /// <summary>Gets or sets a comma-separated list of this supplier's part numbers (including alternates) on the RFQ.</summary>
    public string PartNumbers { get; set; }
}

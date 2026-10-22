namespace TravelMatch.API.DTOs.Admin;

/// <summary>Aggregate counts shown on the admin dashboard.</summary>
public class AdminDashboardSummaryDto
{
    public int TotalUsers { get; set; }

    public int TotalTourists { get; set; }

    public int TotalGuides { get; set; }

    public int TotalOrganizers { get; set; }

    public int TotalTripRequests { get; set; }

    public int TotalOrganizedTrips { get; set; }

    public int TotalRegistrations { get; set; }

    public int TotalProposals { get; set; }

    public int TotalPayments { get; set; }

    public int TotalOpenDisputes { get; set; }

    public int TotalPendingGuideVerifications { get; set; }
}

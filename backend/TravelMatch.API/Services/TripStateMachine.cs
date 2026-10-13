using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public static class TripStateMachine
{
    private static readonly IReadOnlyDictionary<TripRequestStatus, HashSet<TripRequestStatus>> ValidTransitions
        = new Dictionary<TripRequestStatus, HashSet<TripRequestStatus>>
    {
        [TripRequestStatus.Pending] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.ProposalsReceived,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.ProposalsReceived] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.Matched,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.Matched] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.PaymentPending,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.PaymentPending] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.Planned,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.Planned] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.InProgress,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.InProgress] = new HashSet<TripRequestStatus>
        {
            TripRequestStatus.Completed,
            TripRequestStatus.Cancelled
        },
        [TripRequestStatus.Completed] = new HashSet<TripRequestStatus>(),
        [TripRequestStatus.Cancelled] = new HashSet<TripRequestStatus>()
    };

    public static bool CanTransition(TripRequestStatus from, TripRequestStatus to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static IReadOnlySet<TripRequestStatus> GetAllowedTransitions(TripRequestStatus from)
    {
        return ValidTransitions.TryGetValue(from, out var allowed)
            ? allowed
            : new HashSet<TripRequestStatus>();
    }

    public static string GetTransitionError(TripRequestStatus from, TripRequestStatus to)
    {
        if (!ValidTransitions.ContainsKey(from))
        {
            return $"Invalid current status: {from}";
        }

        var allowed = ValidTransitions[from];
        if (allowed.Contains(to))
        {
            return string.Empty;
        }

        return $"Cannot transition from {from} to {to}. Allowed transitions: {string.Join(", ", allowed)}";
    }
}
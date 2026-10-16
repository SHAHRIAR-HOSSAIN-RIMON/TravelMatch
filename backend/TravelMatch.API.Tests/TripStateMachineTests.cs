using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

/// <summary>
/// Tests for TripStateMachine: valid transitions, invalid transitions, and terminal states.
/// </summary>
public class TripStateMachineTests
{
    // ─── Valid transitions ───────────────────────────────────────────────────

    [Theory]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.ProposalsReceived)]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.ProposalsReceived, TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.ProposalsReceived, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.Matched, TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Matched, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.PaymentPending, TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.PaymentPending, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.Planned, TripRequestStatus.InProgress)]
    [InlineData(TripRequestStatus.Planned, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.InProgress, TripRequestStatus.Completed)]
    [InlineData(TripRequestStatus.InProgress, TripRequestStatus.Cancelled)]
    public void ValidTransitions_ReturnNoError(
        TripRequestStatus from,
        TripRequestStatus to)
    {
        var error = TripStateMachine.GetTransitionError(from, to);

        Assert.True(string.IsNullOrEmpty(error),
            $"Expected no error for {from} → {to}, got: {error}");
    }

    // ─── Invalid transitions ─────────────────────────────────────────────────

    [Theory]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.Matched)]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.Planned)]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.InProgress)]
    [InlineData(TripRequestStatus.Pending, TripRequestStatus.Completed)]
    [InlineData(TripRequestStatus.ProposalsReceived, TripRequestStatus.Pending)]
    [InlineData(TripRequestStatus.ProposalsReceived, TripRequestStatus.PaymentPending)]
    [InlineData(TripRequestStatus.Matched, TripRequestStatus.ProposalsReceived)]
    [InlineData(TripRequestStatus.Completed, TripRequestStatus.Pending)]
    [InlineData(TripRequestStatus.Completed, TripRequestStatus.InProgress)]
    [InlineData(TripRequestStatus.Completed, TripRequestStatus.Cancelled)]
    [InlineData(TripRequestStatus.Cancelled, TripRequestStatus.Pending)]
    [InlineData(TripRequestStatus.Cancelled, TripRequestStatus.Completed)]
    public void InvalidTransitions_ReturnErrorMessage(
        TripRequestStatus from,
        TripRequestStatus to)
    {
        var error = TripStateMachine.GetTransitionError(from, to);

        Assert.False(string.IsNullOrEmpty(error),
            $"Expected an error for {from} → {to}, but got none.");
    }

    // ─── GetAllowedTransitions ───────────────────────────────────────────────

    [Fact]
    public void Pending_AllowsProposalsReceivedAndCancelled()
    {
        var allowed = TripStateMachine.GetAllowedTransitions(TripRequestStatus.Pending);

        Assert.Contains(TripRequestStatus.ProposalsReceived, allowed);
        Assert.Contains(TripRequestStatus.Cancelled, allowed);
        Assert.Equal(2, allowed.Count);
    }

    [Fact]
    public void Completed_HasNoAllowedTransitions()
    {
        var allowed = TripStateMachine.GetAllowedTransitions(TripRequestStatus.Completed);

        Assert.Empty(allowed);
    }

    [Fact]
    public void Cancelled_HasNoAllowedTransitions()
    {
        var allowed = TripStateMachine.GetAllowedTransitions(TripRequestStatus.Cancelled);

        Assert.Empty(allowed);
    }

    [Fact]
    public void InProgress_AllowsCompletedAndCancelled()
    {
        var allowed = TripStateMachine.GetAllowedTransitions(TripRequestStatus.InProgress);

        Assert.Contains(TripRequestStatus.Completed, allowed);
        Assert.Contains(TripRequestStatus.Cancelled, allowed);
    }
}

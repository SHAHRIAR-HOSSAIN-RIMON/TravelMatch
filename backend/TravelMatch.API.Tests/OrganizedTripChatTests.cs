using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.Models;
using TravelMatch.API.Services;
using Xunit;

namespace TravelMatch.API.Tests;

public class OrganizedTripChatTests
{
    private static async Task<ApplicationDbContext> Seed()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.AddRange(Enumerable.Range(1, 7).Select(id => new User
        {
            Id = id, FullName = $"Member {id}", Email = $"{id}@test.local", PasswordHash = "test",
            Role = id == 1 || id == 7 ? UserRole.Organizer : id == 2 || id == 6 ? UserRole.Guide : UserRole.Tourist
        }));
        db.OrganizerProfiles.AddRange(new OrganizerProfile { Id = 101, UserId = 1 }, new OrganizerProfile { Id = 107, UserId = 7 });
        db.TouristProfiles.AddRange(new TouristProfile { Id = 103, UserId = 3 }, new TouristProfile { Id = 104, UserId = 4 }, new TouristProfile { Id = 105, UserId = 5 });
        db.OrganizedTrips.Add(new OrganizedTrip { Id = 10, OrganizerId = 101, SelectedGuideId = 2, Title = "Group trip", Status = OrganizedTripStatus.RegistrationOpen });
        db.OrganizedTrips.Add(new OrganizedTrip { Id = 20, OrganizerId = 107, Title = "Other trip", Status = OrganizedTripStatus.RegistrationOpen });
        db.Registrations.AddRange(
            new Registration { Id = 1, OrganizedTripId = 10, TouristId = 103, Status = RegistrationStatus.PendingPayment, NumberOfTravelers = 3 },
            new Registration { Id = 2, OrganizedTripId = 10, TouristId = 104, Status = RegistrationStatus.Confirmed },
            new Registration { Id = 3, OrganizedTripId = 10, TouristId = 105, Status = RegistrationStatus.Cancelled });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task MembersIncludeOnlyOwnerSelectedGuideAndActiveRegistrations()
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        var members = await service.MembersAsync(10);
        Assert.Equal(new[] { 1, 2, 3, 4 }, members.Select(m => m.Id).Order().ToArray());
        Assert.Equal("Guide", members.Single(m => m.Id == 2).Role);
        Assert.Equal("Organizer", members.Single(m => m.Id == 1).Role);
        Assert.Equal(4, members.Count); // Three travellers on an account still mean one member.
    }

    [Theory]
    [InlineData(5)] // cancelled tourist
    [InlineData(6)] // unselected guide
    [InlineData(7)] // another organizer
    [InlineData(99)]
    public async Task OutsidersCannotReadOrSend(int userId)
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HistoryAsync(userId, 10));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SendAsync(userId, 10, "Secret"));
        Assert.Empty(db.OrganizedTripMessages);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task MembersCanSendAndReadPersistedMessages(int userId)
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        var sent = await service.SendAsync(userId, 10, "  Meet at the hotel.  ");
        var history = await service.HistoryAsync(4, 10);
        Assert.Equal(sent.Id, Assert.Single(history.Messages).Id);
        Assert.Equal("Meet at the hotel.", history.Messages[0].Content);
        Assert.Equal(userId, history.Messages[0].SenderId);
    }

    [Fact]
    public async Task CancellationAndDeactivationRevokeHistorySendAndLiveRecipientEligibility()
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        await service.HistoryAsync(3, 10);
        db.Registrations.Single(r => r.Id == 1).Status = RegistrationStatus.Cancelled;
        db.Users.Single(u => u.Id == 4).IsActive = false;
        await db.SaveChangesAsync();
        foreach (var id in new[] { 3, 4 })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HistoryAsync(id, 10));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SendAsync(id, 10, "No access"));
            Assert.DoesNotContain(await service.MembersAsync(10), m => m.Id == id);
        }
    }

    [Fact]
    public async Task ReplacingSelectedGuideRevokesPreviousGuide()
    {
        await using var db = await Seed();
        db.OrganizedTrips.Single(t => t.Id == 10).SelectedGuideId = 6;
        await db.SaveChangesAsync();
        var service = new OrganizedTripChatService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.HistoryAsync(2, 10));
        Assert.Contains(await service.MembersAsync(10), m => m.Id == 6);
    }

    [Theory]
    [InlineData(OrganizedTripStatus.Completed)]
    [InlineData(OrganizedTripStatus.Cancelled)]
    public async Task EndedTripHistoryIsReadOnly(OrganizedTripStatus status)
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        await service.SendAsync(1, 10, "Welcome");
        db.OrganizedTrips.Single(t => t.Id == 10).Status = status;
        await db.SaveChangesAsync();
        Assert.True((await service.HistoryAsync(3, 10)).ReadOnly);
        Assert.Single((await service.HistoryAsync(3, 10)).Messages);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(1, 10, "Too late"));
    }

    [Fact]
    public async Task HistoryPaginationIsStableAndIsolatedByTrip()
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        for (var i = 0; i < 55; i++) await service.SendAsync(1, 10, $"Message {i}");
        await service.SendAsync(7, 20, "Other conversation");
        var recent = await service.HistoryAsync(3, 10);
        var older = await service.HistoryAsync(3, 10, recent.Messages[0].Id);
        Assert.Equal(50, recent.Messages.Count);
        Assert.True(recent.HasMore);
        Assert.Equal(5, older.Messages.Count);
        Assert.False(older.HasMore);
        Assert.Equal(55, older.Messages.Concat(recent.Messages).Select(m => m.Id).Distinct().Count());
        Assert.DoesNotContain(recent.Messages, m => m.Content == "Other conversation");
    }

    [Fact]
    public async Task InvalidMessagesAreNotStored()
    {
        await using var db = await Seed();
        var service = new OrganizedTripChatService(db);
        foreach (var content in new[] { "", "  ", new string('a', 2001) })
            await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(1, 10, content));
        Assert.Empty(db.OrganizedTripMessages);
    }
}

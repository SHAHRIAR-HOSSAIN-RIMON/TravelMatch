using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Data;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public record GroupChatMember(int Id, string Name, string Role);
public record GroupChatMessage(int Id, int SenderId, string SenderName, string SenderRole, string Content, DateTime SentAt);
public record GroupChatHistory(string Title, bool ReadOnly, List<GroupChatMember> Members, List<GroupChatMessage> Messages, bool HasMore);

public class OrganizedTripChatService(ApplicationDbContext context)
{
    public async Task<List<GroupChatMember>> MembersAsync(int tripId)
    {
        var trip = await context.OrganizedTrips.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tripId);
        if (trip is null) return [];
        var ownerId = await context.OrganizerProfiles.Where(p => p.Id == trip.OrganizerId).Select(p => p.UserId).SingleAsync();
        var touristIds = from registration in context.Registrations
                         join profile in context.TouristProfiles on registration.TouristId equals profile.Id
                         where registration.OrganizedTripId == tripId &&
                             (registration.Status == RegistrationStatus.PendingPayment || registration.Status == RegistrationStatus.Confirmed)
                         select profile.UserId;
        return await context.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.Id == ownerId || u.Id == trip.SelectedGuideId || touristIds.Contains(u.Id)))
            .Select(u => new GroupChatMember(u.Id, u.FullName, u.Id == ownerId ? "Organizer" : u.Id == trip.SelectedGuideId ? "Guide" : "Tourist"))
            .ToListAsync();
    }

    private async Task<(OrganizedTrip Trip, List<GroupChatMember> Members)> AuthorizeAsync(int userId, int tripId)
    {
        var members = await MembersAsync(tripId);
        if (!members.Any(m => m.Id == userId)) throw new UnauthorizedAccessException("You are not a current member of this trip chat.");
        var trip = await context.OrganizedTrips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        return (trip, members);
    }

    public async Task<GroupChatHistory> HistoryAsync(int userId, int tripId, int? beforeId = null)
    {
        var (trip, members) = await AuthorizeAsync(userId, tripId);
        var messages = await context.OrganizedTripMessages.AsNoTracking()
            .Where(m => m.OrganizedTripId == tripId && (!beforeId.HasValue || m.Id < beforeId.Value))
            .OrderByDescending(m => m.Id).Take(51)
            .Select(m => new GroupChatMessage(m.Id, m.SenderId, m.Sender.FullName, m.SenderRole, m.Content, m.SentAt)).ToListAsync();
        return new(trip.Title, IsReadOnly(trip), members, messages.Take(50).Reverse().ToList(), messages.Count > 50);
    }

    public async Task<GroupChatMessage> SendAsync(int userId, int tripId, string? content)
    {
        var (trip, members) = await AuthorizeAsync(userId, tripId);
        if (IsReadOnly(trip)) throw new InvalidOperationException("This trip has ended. Chat history is read-only.");
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000) throw new ArgumentException("Messages must contain 1–2000 characters.");
        var sender = members.Single(m => m.Id == userId);
        var message = new OrganizedTripMessage { OrganizedTripId = tripId, SenderId = userId, SenderRole = sender.Role, Content = content };
        context.OrganizedTripMessages.Add(message);
        await context.SaveChangesAsync();
        return new(message.Id, userId, sender.Name, sender.Role, content, message.SentAt);
    }

    private static bool IsReadOnly(OrganizedTrip trip) => trip.Status is OrganizedTripStatus.Completed or OrganizedTripStatus.Cancelled;
}

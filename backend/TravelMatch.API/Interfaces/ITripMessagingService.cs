using TravelMatch.API.DTOs.Messaging;

namespace TravelMatch.API.Interfaces;

public interface ITripMessagingService
{
    Task<bool> IsParticipantAsync(int userId, int proposalId);

    Task<TripMessageResultDto> SendMessageAsync(int senderUserId, SendMessageDto request);

    Task<TripMessageListResultDto> GetMessagesAsync(
        int userId,
        int proposalId,
        int page = 1,
        int pageSize = 50,
        int? beforeId = null);
}


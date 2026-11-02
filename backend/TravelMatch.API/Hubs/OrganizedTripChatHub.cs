using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TravelMatch.API.Hubs;

// Only invalidation signals are delivered here. Message content is fetched through
// the authorized REST endpoint, so stale connections never retain chat access.
[Authorize]
public class OrganizedTripChatHub : Hub { }

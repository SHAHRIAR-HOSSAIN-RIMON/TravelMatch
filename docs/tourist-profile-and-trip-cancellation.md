# Tourist Features — Profile and Trip Request Cancellation

Implementation notes for the two Tourist stories:
View/Update My Profile, and Cancel My Trip Request.

## Endpoints

| Method | Route | Auth | Success |
| --- | --- | --- | --- |
| GET | `/api/tourist/profile` | `[Authorize(Roles = "Tourist")]` | 200 |
| PUT | `/api/tourist/profile` | `[Authorize(Roles = "Tourist")]` | 200 |
| PATCH | `/api/trip-requests/{id}/cancel` | `[Authorize(Roles = "Tourist")]` | 200 |

`TripRequestsController` now carries a second route attribute
(`[Route("api/trip-requests")]`) in addition to `[Route("api/[controller]")]`,
so both `api/TripRequests/...` and `api/trip-requests/...` resolve. The original
`POST /api/TripRequests` route is unchanged.

## Layers touched

```
Controllers/TouristController.cs                     (new)
Controllers/TripRequestController.cs                (Cancel action added)

Services/TouristService.cs                          (new)
Services/TripRequestService.cs                      (CancelAsync added)

Interfaces/ITouristService.cs                       (new)
Interfaces/ITripRequestService.cs                   (CancelAsync added)

DTOs/Tourist/TouristProfileResponseDto.cs           (new)
DTOs/Tourist/UpdateTouristProfileRequestDto.cs      (new)
DTOs/Tourist/TouristProfileResultDto.cs             (new)
DTOs/Tourist/TouristProfileError.cs                 (new)
DTOs/TripRequests/TripRequestError.cs               (4 values appended)
```

No entity was added or renamed, and no `OnModelCreating` change was made, so
**no new EF migration is required**.

## Profile field mapping

The story lists `ProfileImage`, `Bio` and `PreferredTravelStyle`. None of those
exist in the current TravelMatch model, so they are not exposed or accepted.
Only existing columns are used:

| Story field | Model field | Source |
| --- | --- | --- |
| UserId | `User.Id` | User |
| FullName | `User.FullName` | User |
| Email | `User.Email` | User (read only) |
| Phone | `User.PhoneNumber` | User |
| PreferredTravelStyle | `TouristProfile.Preferences` | TouristProfile |
| — | `User.CreatedAt` | User |

`PasswordHash` is never selected into a response DTO. `TouristProfileResponseDto`
has no property that can carry it.

`Email` is intentionally **not** updatable through `PUT /api/tourist/profile`.
It is the login identity and carries a unique index, so changing it belongs in a
separate, verified email-change flow rather than in a self-service profile save.

Adding `ProfileImage`, `Bio` or `PreferredTravelStyle` later requires a model
change plus a migration, and should be decided with the team first.

## Ownership and identity

The client never supplies a user id. Every action reads
`ClaimTypes.NameIdentifier` from the JWT:

```
JWT -> ClaimTypes.NameIdentifier -> ITouristService / ITripRequestService
```

There is no route that accepts a profile id or a tourist id, so one Tourist
cannot address another Tourist's profile or trip request. `CancelAsync`
additionally compares `TripRequest.TouristId` against the authenticated id and
returns `TripRequestError.NotTripRequestOwner` (403) on mismatch.

## Validation

`UpdateTouristProfileRequestDto` uses the project's existing DataAnnotations
convention:

- `FullName` — required, max 100 (matches `User.FullName` column)
- `PhoneNumber` — max 20, optional, must match `^$|^[+]?[0-9\s\-()]{6,20}$`
- `Preferences` — max 1000 (matches `TouristProfile.Preferences` column)

The service repeats the length checks so oversized payloads are rejected with a
`400` and a typed error code rather than surfacing as a database exception.

## Update semantics

`PUT /api/tourist/profile` is a partial update: `FullName` is always written,
`PhoneNumber` and `Preferences` are written only when supplied (non-null). The
existing `TouristProfile` row is located by `UserId` and updated; a new row is
never inserted, so a Tourist can never end up with two profile rows.

## Cancellation rules

`CancelAsync` performs a state change only. No row is deleted, so history,
proposals and audit remain intact.

| Current status | Result |
| --- | --- |
| `Open` | `Status = Cancelled`, `UpdatedAt = UtcNow`, 200 |
| `Cancelled` | 409 Conflict (`TripRequestError.AlreadyCancelled`) |
| `Completed` | 409 Conflict (`TripRequestError.TripRequestCompleted`) |

Ownership is checked before status, so a non-owner probing a request id gets 403
rather than a status hint.

## Interaction with proposals — open question

There is no `Proposal` entity in the codebase yet, so cancellation currently
ignores proposals entirely: a trip request that already has proposals can still
be cancelled, and the proposals are left untouched.

This is deliberate. The story asks not to invent proposal-management rules that
the specification has not defined. Two decisions are left open for the team:

1. Should cancelling a trip request that has proposals be blocked, or allowed
   with proposals auto-rejected?
2. Should a guide be able to see a cancellation once it happens?

When the `Proposal` entity lands, `CancelAsync` in
`Services/TripRequestService.cs` is the single place to add those rules.

## Tests

`backend/TravelMatch.Tests` (xUnit, EF Core InMemory, `WebApplicationFactory`):

```powershell
dotnet test backend/TravelMatch.Tests/TravelMatch.Tests.csproj
```

37 tests, covering the two profile service cases, the six cancel service cases,
and the HTTP layer (200 / 400 / 401 / 403 / 404 / 409), including a Guide being
rejected from the Tourist profile endpoints and Tourist A being denied access to
Tourist B's trip request.

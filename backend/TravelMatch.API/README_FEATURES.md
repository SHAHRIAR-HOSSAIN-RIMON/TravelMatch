# TravelMatch - Tourist Features

This version adds only the assigned Tourist backend features.

## 1. Tourist Profile

`GET /api/tourist/profile`

`PUT /api/tourist/profile`

Requires a JWT with the `Tourist` role. The authenticated user's ID is taken from the JWT; the client cannot choose another user's profile.

Current profile fields used from the existing models:
- User ID
- Profile ID
- Full Name
- Email
- Phone Number
- Preferences

## 2. Trip Request

`GET /api/TripRequests/{id}`

`PUT /api/TripRequests/{id}`

`DELETE /api/TripRequests/{id}`

The DELETE operation performs a cancellation by changing the status to `Cancelled`; it does not physically delete the database record.

A Tourist can only access a TripRequest where `TripRequest.TouristId` matches the authenticated JWT user ID.

Only `Open` trip requests can be updated or cancelled.

## Open in VS Code

1. Extract the ZIP.
2. Open the `backend/TravelMatch.API` folder in VS Code.
3. Open the terminal.
4. Run:

```bash
dotnet restore
dotnet build
dotnet run
```

5. Open the Swagger URL shown in the terminal.

Use `Authorize` in Swagger and enter:

```text
Bearer YOUR_JWT_TOKEN
```

No database migration was added because these features use the existing `User`, `TouristProfile`, and `TripRequest` schema.

# TravelMatch
Intelligent Local Guide & Trip Marketplace

#ER Diagram
<img width="1536" height="1024" alt="image" src="https://github.com/user-attachments/assets/fe292caf-792d-4994-a8cf-a73cb6ae286b" />

## Local development setup

The JWT secret and the PostgreSQL connection string are **never** stored in the repository.
They live in the .NET user-secrets store on the local machine, and the app refuses to start
without them.

```bash
dotnet user-secrets init --project backend/TravelMatch.API
dotnet user-secrets set "JwtSettings:Secret" "replace-with-your-local-jwt-secret" --project backend/TravelMatch.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=travelmatch;Username=postgres;Password=your_local_password" --project backend/TravelMatch.API
```

Create the local database and apply migrations, then run the API:

```bash
dotnet ef database update --project backend/TravelMatch.API
dotnet run --project backend/TravelMatch.API
```

Swagger is available at `/swagger` while the API runs in the Development environment.

`ConnectionStrings:DefaultConnection` can also be supplied through the
`POSTGRES_CONNECTION_STRING` environment variable, which is useful for CI or containers.

## API endpoints

Authentication (all roles):

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`

My profile (any authenticated role):

- `GET /api/profile`
- `PUT /api/profile` — updates `fullName`, `phoneNumber` and the profile section that
  matches the caller's role (`guide`, `tourist` or `organizer`). Sending a section for a
  different role is rejected with `400`.

My trip requests (Tourist):

- `POST /api/trip-requests`
- `GET /api/trip-requests/mine` — optional `status`, `page` and `pageSize` query filters
- `GET /api/trip-requests/mine/{id}`
- `PUT /api/trip-requests/mine/{id}` — only while the request is still `Open`
- `POST /api/trip-requests/mine/{id}/cancel`

Guide browsing:

- `GET /api/trip-requests/open`
- `GET /api/trip-requests/{id}`

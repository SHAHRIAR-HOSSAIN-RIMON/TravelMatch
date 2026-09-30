# TravelMatch
Intelligent Local Guide &amp; Trip Marketplace

#ER Diagram
<img width="1536" height="1024" alt="image" src="https://github.com/user-attachments/assets/fe292caf-792d-4994-a8cf-a73cb6ae286b" />

## Proposal workflow

The proposal UI is served by the ASP.NET API from its `wwwroot` directory. Configure the PostgreSQL `DefaultConnection`, apply migrations, then run the API:

```powershell
dotnet ef database update --project backend/TravelMatch.API
dotnet run --project backend/TravelMatch.API
```

Sign in with an existing account. Verified guides can browse open requests, submit one pending proposal per request, and track submissions under **My proposals**. Tourists can review in-app proposal notifications from **Notifications**. Guide verification status is shown under **Verification**.

Proposal endpoints:

- `GET /api/guide/verification`
- `GET /api/guide/triprequests`
- `GET /api/guide/triprequests/{tripRequestId}`
- `POST /api/triprequests/{tripRequestId}/proposals`
- `GET /api/proposals/mine`
- `GET /api/notifications/mine`
- `PUT /api/notifications/{notificationId}/read`

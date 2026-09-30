# TravelMatch
Intelligent Local Guide &amp; Trip Marketplace

## Guide trip requests

Guide accounts can browse open trip requests at `GET /api/guide/trip-requests`. The endpoint supports destination, trip type, overlapping travel-date range, minimum/maximum budget, and newest/highest/lowest budget sorting. Request details are available at `GET /api/guide/trip-requests/{id}`. Only open requests are returned; tourist identity and contact details are not included.

The Guide browse page is a dependency-free static frontend in `frontend/`. Start the API using the `http` launch profile, then run `py -m http.server 5500 --directory frontend` from the repository root and open `http://localhost:5500`. Sign in with a Guide account. The development API allows this local origin through its CORS settings. Set `Cors:AllowedOrigins` for the deployed frontend origin in other environments.

Apply database migrations before running the API with an existing database: `dotnet ef database update` from `backend/TravelMatch.API`. New trip requests may include `tripType` and `travelPreferences`; existing clients default to `Other` and empty preferences.

Proposal submission and verification document upload are not implemented yet. Unverified Guides can browse, but the page explains that only verified Guides can submit proposals.

#ER Diagram
<img width="1536" height="1024" alt="image" src="https://github.com/user-attachments/assets/fe292caf-792d-4994-a8cf-a73cb6ae286b" />

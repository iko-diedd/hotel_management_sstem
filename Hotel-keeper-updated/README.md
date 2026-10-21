# Hotel Housekeeping Room Status & Availability API

ASP.NET Core 8 Web API with EF Core (Pomelo), MySQL 8, JWT auth and Swagger, plus a static web UI in `wwwroot`.

## Run
1. Create the database and an app user: `database/schema.sql`, then `database/create_app_user.sql`
   (or let the API create the schema on first start; see *Database* below).
2. Copy `.env.example` values into your environment (at minimum `DB_*` or `ConnectionStrings__Default`, `Jwt__Key`).
3. `dotnet run --project src/HousekeepingApi` then open `http://localhost:5000` (see `launchSettings.json`).
   The first start creates an admin from `Seed__AdminUsername` / `Seed__AdminPassword`
   (a random password is logged once if none is set).

## Pages
`index.html` public availability · `login.html` staff sign-in · `assistant.html` · `housekeeper.html` ·
`admin.html` rooms and staff · `dashboard.html` occupancy, notice and overdue stats (admin) · `audit.html` audit log (admin).

## Database
- With no EF migrations in the project, startup builds the schema from the model (`EnsureCreated`).
- For production, generate migrations once and commit them:
  `dotnet tool install --global dotnet-ef`
  `dotnet ef migrations add InitialCreate --project src/HousekeepingApi`
  The app user then needs DDL rights while migrating (see the commented grant in `create_app_user.sql`).
  A development database created by `EnsureCreated` must be dropped (or baselined) before switching to migrations.

## Configuration
| Setting | Purpose |
|---|---|
| `Hotel__TimeZone` | IANA zone used for "completed today" (e.g. `Asia/Manila`) |
| `Hotel__OverdueNoticeMinutes` | age at which an open notice counts as overdue (default 30) |
| `ForwardedHeaders__Enabled` | set `true` only behind a trusted reverse proxy, so rate limiting and lockout see real client IPs |

## Security notes
- Failed-login lockout is per username **and** IP, so one client cannot lock others out.
- The public endpoints expose availability only (no occupied/dirty/cleaning detail).

## Tests
`dotnet test tests/HousekeepingApi.Tests`

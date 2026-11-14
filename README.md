# SpendMate

SpendMate is a family-oriented expense tracking application built with ASP.NET Core, Vue.js, and PostgreSQL.

The project is an MVP focused on recording, managing, and reviewing household expenses through a web interface, with support for personal and shared family workspaces.

## Features

- User registration and login with cookie-based authentication.
- Personal and Shared Family workspaces.
- Create Shared Families and manage family memberships.
- Switch between available Family workspaces without logging out.
- Persistent Active Family selection across sessions.
- Automatic fallback to Personal Family when membership in the active Shared Family is removed.
- Family-scoped expense access enforced by application services.
- Create, edit, delete, and view expenses.
- Expense categories and descriptions.
- Date range filters: this month, last month, and custom range.
- Dashboard summaries: total spending, transaction count, average expense, and category breakdown.
- IDR currency formatting.
- Chat Demo for simulating expense entry through a chat-like interface.

## Technology Stack

### Backend

- .NET 10
- ASP.NET Core Minimal APIs
- Entity Framework Core 10
- PostgreSQL 17
- Modular Monolith architecture
- Cookie-based authentication
- CSRF protection

### Frontend

- Vue 3
- TypeScript
- Vite
- Vitest

### Testing

- xUnit
- Moq
- ASP.NET Core integration testing
- Testcontainers for PostgreSQL

## Project Structure

    SpendMate/
    ├── .github/
    │   └── workflows/
    ├── src/
    │   ├── backend/
    │   │   ├── SpendMate.Api/
    │   │   ├── SpendMate.Application/
    │   │   ├── SpendMate.Domain/
    │   │   └── SpendMate.Infrastructure/
    │   └── frontend/
    │       └── spendmate-web/
    ├── tests/
    │   ├── SpendMate.IntegrationTests/
    │   └── SpendMate.UnitTests/
    ├── docker-compose.yml
    └── SpendMate.slnx

## Prerequisites

- .NET SDK 10
- Node.js 22
- Docker Desktop or another Docker-compatible runtime
- Entity Framework Core CLI 10.0.12

Install the EF Core CLI if needed:

    dotnet tool install --global dotnet-ef --version 10.0.12

## Getting Started

Run backend commands from the repository root.

### 1. Start PostgreSQL

Set a local development password and start the database:

    $env:POSTGRES_PASSWORD = "your-local-password"
    docker compose up -d

PostgreSQL is exposed on localhost:5433.

### 2. Configure the API connection string

Set the connection string in the same PowerShell session:

    $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=spendmate;Username=spendmate;Password=your-local-password"

Do not commit credentials or production secrets.

### 3. Apply database migrations

    dotnet ef database update --project .\src\backend\SpendMate.Infrastructure\SpendMate.Infrastructure.csproj --startup-project .\src\backend\SpendMate.Api\SpendMate.Api.csproj

Development startup does not automatically apply migrations. Apply them before starting the API.

### 4. Start the backend

Run the HTTPS launch profile:

    dotnet run --project .\src\backend\SpendMate.Api\SpendMate.Api.csproj --launch-profile https

Development launch URLs:

- HTTP: http://localhost:5267
- HTTPS: https://localhost:7012

The development seeder creates a development user and a Personal Family workspace:

- User: developer@spendmate.local
- Family: SpendMate Demo Family
- Membership role: Owner

The seeder does not create sample expenses.

### 5. Start the frontend

Open another terminal:

    cd .\src\frontend\spendmate-web
    npm ci
    npm run dev

Vite proxies `/api` and `/health` requests to the backend at `https://localhost:7012`.

Open the frontend at the local URL displayed by Vite.

## Authentication and Family Workspaces

SpendMate uses cookie-based authentication with HTTP-only and secure cookies, together with CSRF protection for state-changing API requests.

Each registered user starts with a Personal Family workspace, which is also their default Active Family.

Users can create Shared Family workspaces and switch between Family workspaces they belong to.

The selected Active Family is persisted in the database through the user's `ActiveFamilyId` and restored across sessions.

When a user's membership in their currently active Shared Family is removed, the application automatically switches them back to their Personal Family.

Expense operations are scoped to the requested Family and require authenticated access and valid Family membership.

## Running Tests

### Backend

Run from the repository root:

    dotnet restore .\SpendMate.slnx
    dotnet build .\SpendMate.slnx --configuration Release --no-restore
    dotnet test .\SpendMate.slnx --configuration Release --no-build

Integration tests use Testcontainers and require Docker.

### Frontend

Run from `src/frontend/spendmate-web`:

    npm ci
    npm run test
    npm run build

## Database

The database schema is managed through EF Core migrations.

Core entities include:

- Users
- Families
- FamilyMemberships
- Expenses

The `Users` table includes `ActiveFamilyId`, which references the user's currently selected Family workspace.

Family membership records define the relationship between users and Family workspaces, including membership roles.

Apply all migrations before running the application against a database.

## Current Limitations

- Chat Demo is a simulated chat-like interface, not a WhatsApp or AI integration.
- Production deployment, infrastructure, and operational configuration require further work.
- Additional expense insights and reporting capabilities are still planned.

## Future Improvements

- Actual messaging platform integration.
- More advanced expense insights and reporting.
- Production deployment and operational hardening.

## License

No license has been specified yet.

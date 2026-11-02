# SpendMate

SpendMate is a family-oriented expense tracking application built with ASP.NET Core, Vue.js, and PostgreSQL.

The project is an MVP focused on recording, managing, and reviewing household expenses through a web interface.

## Features

- Create, edit, delete, and view expenses.
- Expense categories and descriptions.
- Date range filters: this month, last month, and custom range.
- Dashboard summaries: total spending, transaction count, average expense, and category breakdown.
- IDR currency formatting.
- Chat Demo for simulating expense entry through a chat-like interface.
- Family-scoped expense access enforced by application services.

## Technology Stack

### Backend
- .NET 10
- ASP.NET Core Minimal APIs
- Entity Framework Core 10
- PostgreSQL 17
- Modular Monolith architecture

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

Run commands from the repository root.

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

    dotnet run --project .\src\backend\SpendMate.Api\SpendMate.Api.csproj

Development launch URLs:

- HTTP: http://localhost:5267
- HTTPS: https://localhost:7012

The development seeder creates:

- User: developer@spendmate.local
- Family: SpendMate Demo Family
- Membership role: Owner

The seeder does not create sample expenses.

### 5. Start the frontend

Open another terminal:

    cd .\src\frontend\spendmate-web
    npm ci
    npm run dev

Vite proxies /api and /health requests to http://localhost:5267.

## Running Tests

### Backend

Run from the repository root:

    dotnet restore .\SpendMate.slnx
    dotnet build .\SpendMate.slnx --configuration Release --no-restore
    dotnet test .\SpendMate.slnx --configuration Release --no-build

Integration tests use Testcontainers and require Docker.

### Frontend

Run from src/frontend/spendmate-web:

    npm ci
    npm run test
    npm run build

## Database

The initial EF Core migration creates:

- Users
- Families
- FamilyMemberships
- Expenses

The database schema is managed through EF Core migrations.

## Current Limitations

- Authentication is not implemented. Development uses a static identity.
- The frontend uses a fixed development user and family context.
- Expense API endpoints are currently mapped only in Development and Testing environments.
- Chat Demo is a simulated chat-like interface, not a WhatsApp or AI integration.
- Family membership exists in the backend data model, but production-ready multi-user collaboration is not implemented.

## Future Improvements

- Authentication and authorization for real users.
- Family member management and invitations.
- Production API and identity configuration.
- Actual messaging platform integration.
- More advanced expense insights and reporting.

## License

No license has been specified yet.

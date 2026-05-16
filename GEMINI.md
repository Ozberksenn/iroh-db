# Iroh Backend (iroh-be)

## Project Overview
Iroh-be is a modern Web API built with **.NET 9.0**, serving as the backend for the Iroh management system (likely for restaurant or cafe management). It uses **Entity Framework Core** with a **PostgreSQL** database to manage entities like Tables, Companies, and Customers.

### Main Technologies
- **Framework:** .NET 9.0 (ASP.NET Core)
- **Database:** PostgreSQL (via `Npgsql.EntityFrameworkCore.PostgreSQL`)
- **ORM:** Entity Framework Core
- **API Documentation:** Swagger/OpenAPI (Swashbuckle)

### Architecture
The project follows a standard **Controller-Service-Repository** (or DbContext) pattern:
- **Controllers:** Handle HTTP requests and define API endpoints.
- **Services:** Contain business logic and interact with the data layer.
- **Models:**
    - **Entities:** Represent database tables (e.g., `Table`, `Company`, `Customer`), inheriting from `BaseEntity`.
    - **DTOs:** Data Transfer Objects used for API requests and responses.
    - **CustomResponses:** A standardized wrapper for API responses (`CustomResponse<T>`).
- **Data:** `AppDbContext` manages the database connection and entity mappings.

---

## Building and Running

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL database

### Key Commands
- **Restore dependencies:** `dotnet restore`
- **Build project:** `dotnet build`
- **Run project:** `dotnet run` (Starts on `http://localhost:5034` or `https://localhost:7296` by default)
- **Watch mode:** `dotnet watch run` (Automatic reload on changes)
- **Database Migrations:** (Standard EF Core commands)
    - `dotnet ef migrations add <MigrationName>`
    - `dotnet ef database update`

---

## Development Conventions

### Coding Style
- **Namespaces:** Use `Iroh.*` (e.g., `Iroh.Controllers`, `Iroh.Services`, `Iroh.Models.Entities`).
- **Dependency Injection:** Services are registered as `Scoped` in `Program.cs`.
- **Entities:** 
    - Always inherit from `BaseEntity` (provides `id`).
    - Use Data Annotations (`[Table]`, `[Column]`) to map to specific PostgreSQL schema and table names.
    - Property names in C# are typically camelCase, while database columns are often mapped to lowercase names (e.g., `isdeleted`).

### API Conventions
- **Standardized Responses:** All API responses should be wrapped in `CustomResponse<T>` to provide consistent `Success`, `Message`, and `Data` fields.
- **Routing:** Controllers use the `[ApiController]` attribute and `[Route("api/[controller]")]` convention.
- **DTOs:** Use specific DTOs (e.g., `TableCreateDto`, `TableUpdateDto`) for POST and PUT operations instead of using Entity models directly.

### Database
- PostgreSQL is the primary database.
- Connection string is configured in `appsettings.json` under `DefaultConnection`.

---

## TODO / Future Improvements
- [ ] Add unit and integration tests.
- [ ] Implement authentication and authorization.
- [ ] Expand logging and error handling middleware.

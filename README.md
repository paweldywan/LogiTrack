# LogiTrack

A production-ready logistics and inventory management API built with ASP.NET Core 9.0.

## Features

- **RESTful API** for inventory and order management
- **Authentication & Authorization** using ASP.NET Core Identity with JWT tokens
- **Role-based access control** (User, Manager roles)
- **Pagination** for collection endpoints
- **Caching** with in-memory cache and smart invalidation
- **Rate limiting** (fixed and sliding window)
- **API versioning** via URL segment and header
- **Structured logging** with Serilog
- **Health checks** endpoint
- **Soft delete** with audit fields
- **Request validation** using FluentValidation
- **Swagger/OpenAPI** documentation with JWT support

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQLite (included, no installation required)

### Running the Application

```bash
# Clone the repository
git clone https://github.com/paweldywandev/LogiTrack.git
cd LogiTrack

# Apply database migrations
dotnet ef database update --project LogiTrack.Data --startup-project LogiTrack.Web

# Run the application
dotnet run --project LogiTrack.Web
```

The API will be available at `https://localhost:5001` (or `http://localhost:5000`).

### Running Tests

```bash
dotnet test
```

## API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register a new user |
| POST | `/api/auth/login` | Login and receive JWT token |

### Inventory

| Method | Endpoint | Description | Authorization |
|--------|----------|-------------|---------------|
| GET | `/api/inventory` | Get all items (paginated) | User, Manager |
| GET | `/api/inventory/{id}` | Get item by ID | User, Manager |
| POST | `/api/inventory` | Create new item | Manager |
| PUT | `/api/inventory/{id}` | Update item | Manager |
| DELETE | `/api/inventory/{id}` | Delete item (soft delete) | Manager |

### Orders

| Method | Endpoint | Description | Authorization |
|--------|----------|-------------|---------------|
| GET | `/api/order` | Get all orders (paginated) | User, Manager |
| GET | `/api/order/{id}` | Get order by ID | User, Manager |
| POST | `/api/order` | Create new order | Manager |
| PUT | `/api/order/{id}` | Update order | Manager |
| DELETE | `/api/order/{id}` | Delete order (soft delete) | Manager |

### Health

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check endpoint |

## API Versioning

The API supports versioning via:

- **URL segment**: `/api/v1/inventory`
- **Header**: `X-Api-Version: 1.0`

## Pagination

Collection endpoints support pagination with query parameters:

```
GET /api/inventory?page=1&pageSize=20
```

Response format:
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalItems": 100,
  "totalPages": 5,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## Authentication

1. Register a new user:
```bash
curl -X POST https://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com", "password": "Password123!"}'
```

2. Login to get a JWT token:
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com", "password": "Password123!"}'
```

3. Use the token in subsequent requests:
```bash
curl https://localhost:5001/api/inventory \
  -H "Authorization: Bearer <your-token>"
```

## Configuration

Key configuration options in `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "your-secret-key",
    "Issuer": "LogiTrack",
    "Audience": "LogiTrack"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

## Project Structure

```
LogiTrack/
├── LogiTrack.Web/           # API layer (controllers, middleware, validators)
├── LogiTrack.Domain/        # Domain models and interfaces
├── LogiTrack.Data/          # Data access layer (EF Core, repositories)
├── LogiTrack.Web.Tests/     # Integration and unit tests for Web
├── LogiTrack.Data.Tests/    # Unit tests for Data layer
└── LogiTrack.Domain.Tests/  # Unit tests for Domain layer
```

## Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        API Layer                                │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ Controllers │  │ Middleware  │  │ Validators              │  │
│  │             │  │ • Exception │  │ • InventoryItemValidator│  │
│  │ • Inventory │  │ • CorrelId  │  │ • OrderValidator        │  │
│  │ • Order     │  │ • RateLimit │  └─────────────────────────┘  │
│  │ • Auth      │  └─────────────┘                               │
│  └──────┬──────┘                                                │
└─────────┼───────────────────────────────────────────────────────┘
          │
┌─────────▼───────────────────────────────────────────────────────┐
│                      Domain Layer                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ InventoryItem   │  │ Order           │  │ IAuditableEntity│  │
│  │ • ItemId        │  │ • OrderId       │  │ • CreatedAt     │  │
│  │ • Name          │  │ • CustomerName  │  │ • UpdatedAt     │  │
│  │ • Quantity      │  │ • DatePlaced    │  │ • IsDeleted     │  │
│  │ • Location      │  │ • Items[]       │  │ • DeletedAt     │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
          │
┌─────────▼───────────────────────────────────────────────────────┐
│                       Data Layer                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ LogiTrackContext│  │ Repositories    │  │ Configurations  │  │
│  │ • DbSet<>       │  │ • IInventory... │  │ • Entity configs│  │
│  │ • SaveChanges   │  │ • IOrder...     │  │ • Query filters │  │
│  │ • Audit logic   │  └─────────────────┘  └─────────────────┘  │
│  └─────────────────┘                                            │
└─────────────────────────────────────────────────────────────────┘
          │
┌─────────▼───────────────────────────────────────────────────────┐
│                       SQLite Database                           │
└─────────────────────────────────────────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| **Clean Architecture (3-layer)** | Separation of concerns with Web, Domain, and Data layers. Domain has no dependencies on infrastructure. |
| **Repository Pattern** | Abstracts data access, enables unit testing with mocks, and provides a clean API for CRUD operations. |
| **JWT Authentication** | Stateless authentication suitable for APIs. Tokens contain claims for role-based authorization. |
| **Soft Delete** | Records are marked as deleted (`IsDeleted = true`) rather than removed. Preserves audit trail and enables recovery. Implemented via EF Core global query filters. |
| **Version-based Cache Invalidation** | Cache keys include a version number. Incrementing the version effectively invalidates all cached pages without tracking individual keys. |
| **Primary Constructor DI** | C# 12 feature reduces boilerplate. Constructor parameters are captured as fields automatically. |
| **FluentValidation** | Separates validation logic from controllers. Rules are testable and composable. |
| **Serilog with Correlation IDs** | Structured logging enables searching/filtering. Correlation IDs trace requests across services. |
| **SQLite for Development** | Zero-configuration database, portable, suitable for development and testing. Easily swappable to SQL Server/PostgreSQL for production. |

### Request Flow

```
Request → Rate Limiter → Correlation ID Middleware → Authentication
    → Authorization → Controller → Validation → Repository → Database
    → Response (with cache if applicable)
```

### Security Layers

1. **Authentication**: JWT Bearer tokens with configurable expiration
2. **Authorization**: Role-based policies (User can read, Manager can write)
3. **Rate Limiting**: Prevents abuse with fixed and sliding window limits
4. **Input Validation**: FluentValidation rules on all write operations
5. **CORS**: Configurable allowed origins

## Technologies

- ASP.NET Core 9.0
- Entity Framework Core 9.0
- ASP.NET Core Identity
- SQLite
- Serilog
- FluentValidation
- xUnit & NSubstitute

## License

This project is licensed under the MIT License.


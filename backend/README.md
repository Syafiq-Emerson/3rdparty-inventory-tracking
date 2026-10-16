# Industrial Inventory Management System - ASP.NET Core & MySQL Backend

## Enterprise Architecture Overview

This backend is built strictly to the company IT specification:
- **Framework**: ASP.NET Core 8.0 Web API (C# 12, .NET LTS)
- **Database**: MySQL 8.0+
- **ORM**: Entity Framework Core 8.0 with `Pomelo.EntityFrameworkCore.MySql`
- **Security & Integrity**:
  - Unique database constraints on `serial_number`, `tag_number`, `location_code`, and `personnel_code`.
  - Atomic database transactions (`BeginTransactionAsync`) guaranteeing that inventory creation and its initial `inventory_movements` record commit or roll back together.
  - Foreign key constraints set to `RESTRICT` on delete to protect historical data integrity.
  - No database entities exposed as public API contracts; all communication uses strongly typed DTO records.

---

## Database Setup & Migration

### Option 1: Execute Raw MySQL DDL Script
Execute `/backend/schema.sql` directly on your MySQL 8.0+ instance:
```bash
mysql -u root -p < /backend/schema.sql
```

### Option 2: Apply EF Core Migrations
If using the .NET CLI migration workflow:
```bash
cd backend/IndustrialInventory.Api
dotnet tool install --global dotnet-ef
dotnet ef database update
```

---

## Local Development & Compilation

1. **Prerequisites**:
   - .NET 8.0 SDK (`dotnet --version` >= 8.0.0)
   - MySQL 8.0 Server running on port 3306

2. **Configure Connection String**:
   Edit `appsettings.json` or `appsettings.Development.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=industrial_inventory;User=inventory_app;Password=YourSecurePassword;CharSet=utf8mb4;"
   }
   ```

3. **Build and Run**:
   ```bash
   dotnet restore
   dotnet build
   dotnet run --launch-profile https
   ```
   The API will start at `https://localhost:7001` and `http://localhost:5000` with Swagger UI at `/swagger`.

---

## API Endpoints Implemented

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/dashboard/summary` | Live metrics, active counts, location inventory breakdown |
| `GET` | `/api/inventory` | Search & paginate inventory items |
| `GET` | `/api/inventory/{id}` | Retrieve individual inventory item details |
| `GET` | `/api/inventory/search?q={query}` | Search by Serial Number or Tag Number |
| `POST` | `/api/inventory` | Register item + movement history in an atomic transaction |
| `GET` | `/api/inventory/{id}/history` | Retrieve full location movement history |
| `GET` | `/api/locations` | List store locations (with `?activeOnly=true` filter) |
| `POST` | `/api/locations` | Add new store location |
| `PUT` | `/api/locations/{id}` | Edit store location |
| `PATCH`| `/api/locations/{id}/status` | Toggle active/deactive status |
| `GET` | `/api/personnel` | List personnel (with `?activeOnly=true` filter) |
| `POST` | `/api/personnel` | Add new personnel record |
| `PUT` | `/api/personnel/{id}` | Edit personnel record |
| `PATCH`| `/api/personnel/{id}/status` | Toggle active/deactive status |

---

## Production Security Checklist
- [ ] Ensure database credentials are provided via AWS Secrets Manager, Azure Key Vault, or Docker secrets (never plain text).
- [ ] Enforce HTTPS in production via reverse proxy (Nginx / Cloudflare / IIS / Kestrel TLS).
- [ ] Restrict CORS origins strictly to internal corporate domains/IPs.
- [ ] Keep connection pooling with `EnableRetryOnFailure` enabled for network resilience.

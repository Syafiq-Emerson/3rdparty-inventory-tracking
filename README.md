# Industrial Inventory Management System

[![ASP.NET Core 8.0](https://img.shields.io/badge/Backend-ASP.NET%20Core%208.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Entity Framework Core](https://img.shields.io/badge/ORM-EF%20Core%208.0%20Pomelo-blue)](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql)
[![MySQL 8.0](https://img.shields.io/badge/Database-MySQL%208.0-4479A1?logo=mysql&logoColor=white)](https://www.mysql.com/)
[![Vite](https://img.shields.io/badge/Frontend-Vite%20%7C%20TypeScript-646CFF?logo=vite&logoColor=white)](https://vitejs.dev/)
[![Tailwind CSS](https://img.shields.io/badge/Styling-Tailwind%20CSS-06B6D4?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)

A mission-critical enterprise web application designed for industrial manufacturing and process control facilities. The system tracks individual physical assets (control valves, pressure transmitters, coriolis meters, sensors) using two distinct linear barcodes on a single physical label: **Serial Number** and **Tag Number**.

---

## Table of Contents

1. [System Architecture & Stack](#system-architecture--stack)
2. [UI/UX Design Inspiration](#uiux-design-inspiration)
3. [Key Features & Workflows](#key-features--workflows)
   - [Handheld Scanner Workflow](#1-handheld-scanner-workflow-usbbluetooth)
   - [Phone Camera Barcode Scanner](#2-phone-camera-barcode-scanner)
   - [Atomic Registration & Duplicate Conflict Handling](#3-atomic-registration--duplicate-conflict-handling)
   - [Live Telemetry Dashboard](#4-live-telemetry-dashboard)
   - [Inventory Catalog & Filterable Search](#5-inventory-catalog--filterable-search)
   - [Master Data Settings](#6-master-data-settings)
4. [Database Design & Schema](#database-design--schema)
5. [REST API Specifications](#rest-api-specifications)
6. [Repository Structure](#repository-structure)
7. [Installation & Deployment](#installation--deployment)
   - [Running the Interactive Workstation (Vite)](#1-running-the-interactive-workstation)
   - [Running the Production ASP.NET Core API](#2-running-the-production-aspnet-core-api)
   - [Deploying the MySQL Database](#3-deploying-the-mysql-database)
8. [Security & Compliance](#security--compliance)

---

## System Architecture & Stack

Built strictly to corporate IT standards for industrial operations:

- **Frontend**: Vue 3 / React SPA on Vite, TypeScript, Tailwind CSS, Lucide icons, `@zxing/library` for optical linear barcode decoding.
- **Backend**: ASP.NET Core 8.0 Web API in C# 12, targeting .NET 8.0 LTS.
- **Database**: MySQL 8.0+ with utf8mb4 encoding and InnoDB engine.
- **ORM**: Entity Framework Core 8.0 with `Pomelo.EntityFrameworkCore.MySql`.
- **API Communication**: REST over HTTP in local development; HTTPS with strict transport security in production.
- **Hosting**: Internal corporate network, on-premise application servers, or enterprise container orchestration (Docker / Kubernetes).

---

## UI/UX Design Inspiration

The user interface follows the industrial corporate aesthetic of **Emerson AMS (Asset Management Systems)**:
- **Color Palette**: 60% clean white canvas (`#FFFFFF`), 30% structural light-grey surfaces (`#F4F6F9`), and 10% restrained corporate navy accents (`#004B87`, `#0B1E36`).
- **Data Density**: Compact row heights, hairline dividers (`border-slate-200`), and tabular monospace figures (`font-mono tabular-nums`).
- **Zero AI-Slop Discipline**: No decorative pulsing dots, no pill badge sandwiches, no floating pseudo-metrics, and no code-comment headings.
- **Responsive Layout**: Persistent desktop sidebar (`w-64`) with an adaptive touch drawer on mobile and tablet viewports.

---

## Key Features & Workflows

### 1. Handheld Scanner Workflow (USB/Bluetooth)
Industrial linear barcode scanners operate in **keyboard wedge mode**, firing rapid keypress sequences (< 50ms interval) terminated by an `Enter` key:
- **Order-Independent Capture**: Accepts either the Serial Number first or the Tag Number first.
- **Intelligent Classification**: Automatically classifies the barcode using configurable regular expressions:
  - **Tag Number** (ISA-5.1 process loops, e.g., `PV-16501`, `FT-10402`, `LT-20415`, containing hyphens).
  - **Serial Number** (Manufacturer chassis identifiers, e.g., `F005327261`, `S991048210`, alphanumeric strings).
- **Overwrite Protection**: Retains the first captured value while waiting for the second. Never silently overwrites an existing field.
- **Manual Swap & Correction**: One-click field swapping if an ambiguous barcode was classified into the opposite slot.

### 2. Phone Camera Barcode Scanner
- **Dual-Slot HUD**: Live heads-up display showing Slot 1 (Serial Number) and Slot 2 (Tag Number).
- **Simultaneous or Sequential Scan**: Attempts to decode both barcodes from the label in one view; if only one is detected, it locks that slot and guides the technician to align the second barcode.
- **Industrial Viewfinder**: Aiming reticle with laser guide animation.
- **Hardware Controls**: Torch/flashlight toggle and front/rear camera switcher.
- **Manual Fallback**: Integrated keyboard inputs for damaged or unreadable barcode labels.

### 3. Atomic Registration & Duplicate Conflict Handling
- **Four Mandatory Fields**: Serial Number, Tag Number, Store Location, and Scan By (technician).
- **Dynamic Dropdowns**: Populated in real time from active database records (`GET /api/locations?activeOnly=true` and `GET /api/personnel?activeOnly=true`).
- **Atomic Transactions**: Creating an inventory item commits both the `inventory_items` record and its initial `inventory_movements` (INTAKE) record in a single database transaction.
- **Duplicate Prevention**: If either the Serial Number or Tag Number already exists:
  - Rejects the submission with HTTP 409 Conflict.
  - Displays the **Duplicate Conflict Modal**, showing the existing item's Serial Number, Tag Number, Current Store Location, Registered-By Technician, and Intake Timestamp.

### 4. Live Telemetry Dashboard
- **Live Database Tallies**: Total registered units, active store locations, active personnel, and recent intake events.
- **Location Distribution Chart**: Bar distribution chart displaying units housed in each storage bay.
- **Recent Registrations**: Quick-access data table with monospace identifiers and one-click inspection.

### 5. Inventory Catalog & Filterable Search
- **Server-Side Querying**: Fast search across Serial Numbers and Tag Numbers without loading the full catalog into the browser.
- **Location Filtering**: Filter inventory by specific warehouse, calibration lab, or fabrication shop.
- **Asset Detail Modal**: Inspects full equipment specifications and the chronological **Chain of Custody Ledger** (`inventory_movements`).
- **CSV Data Export**: One-click download of the filtered dataset for enterprise ERP integration.

### 6. Master Data Settings
- **Store Locations**: Add, edit, and deactivate locations. Enforces unique location codes (e.g., `WH-A-01`).
- **Personnel Directory**: Add, edit, and deactivate technician accounts. Enforces unique personnel badge codes (e.g., `EMP-1042`).
- **Non-Destructive Deactivation**: Master records referenced by historical inventory movements cannot be hard-deleted, ensuring perpetual audit integrity.

---

## Database Design & Schema

```
+--------------------+            +-----------------------+            +--------------------+
|  store_locations   |            |    inventory_items    |            |     personnel      |
+--------------------+            +-----------------------+            +--------------------+
| id (PK)            |<---+       | id (PK)               |      +---->| id (PK)            |
| location_code (UQ) |    |       | serial_number (UQ)    |      |     | personnel_code (UQ)|
| location_name      |    +-------| location_id (FK)      |      |     | name               |
| is_active          |            | registered_by_id (FK) |------+     | is_active          |
| created_at         |            | created_at            |            | created_at         |
| updated_at         |            | updated_at            |            | updated_at         |
+--------------------+            +-----------------------+            +--------------------+
          ^                                   ^                                   ^
          |                                   |                                   |
          |                       +-----------------------+                       |
          |                       |  inventory_movements  |                       |
          |                       +-----------------------+                       |
          |                       | id (PK)               |                       |
          |                       | inventory_item_id (FK)|-----------------------+
          +-----------------------| to_location_id (FK)   |
          |                       | from_location_id (FK) |
          +-----------------------| performed_by_id (FK)  |
                                  | event_type            |
                                  | moved_at              |
                                  +-----------------------+
```

### Table Definitions

| Table | Primary Key | Unique Constraints | Foreign Keys | Purpose |
|---|---|---|---|---|
| `store_locations` | `id` (VARCHAR 36) | `location_code` | None | Storage bays, clean rooms, quarantine docks |
| `personnel` | `id` (VARCHAR 36) | `personnel_code` | None | Accountable technicians and supervisors |
| `inventory_items` | `id` (VARCHAR 36) | `serial_number`, `tag_number` | `location_id`, `registered_by_personnel_id` | Physical equipment registry |
| `inventory_movements`| `id` (VARCHAR 36) | None | `inventory_item_id`, `to_location_id`, `from_location_id`, `performed_by_personnel_id` | Historical chain-of-custody audit trail |

---

## REST API Specifications

| Method | Endpoint | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/dashboard/summary` | Live KPI tallies & location distribution breakdown | 200 |
| `GET` | `/api/inventory` | Paginated list of inventory items (`?locationId=...`) | 200 |
| `GET` | `/api/inventory/{id}` | Individual inventory item specifications | 200, 404 |
| `GET` | `/api/inventory/search` | Search inventory by Serial or Tag (`?q=...&locationId=...`) | 200 |
| `POST`| `/api/inventory` | Register item & initial movement record in an atomic transaction | 201, 400, 409 |
| `GET` | `/api/inventory/{id}/history` | Chronological chain-of-custody movement ledger | 200, 404 |
| `GET` | `/api/locations` | List store locations (`?activeOnly=true`) | 200 |
| `POST`| `/api/locations` | Add new store location | 201, 400, 409 |
| `PUT` | `/api/locations/{id}` | Update store location code and name | 200, 400, 404, 409 |
| `PATCH`| `/api/locations/{id}/status`| Toggle store location active/inactive state | 200, 404 |
| `GET` | `/api/personnel` | List personnel directory (`?activeOnly=true`) | 200 |
| `POST`| `/api/personnel` | Add new personnel record | 201, 400, 409 |
| `PUT` | `/api/personnel/{id}` | Update personnel ID and full name | 200, 400, 404, 409 |
| `PATCH`| `/api/personnel/{id}/status`| Toggle personnel active/inactive state | 200, 404 |

---

## Repository Structure

```
├── backend/
│   ├── schema.sql                                     # MySQL 8.0+ DDL with unique constraints, indexes & seeds
│   ├── README.md                                      # Setup, EF Core migration & deployment instructions
│   └── IndustrialInventory.Api/
│       ├── IndustrialInventory.Api.csproj            # ASP.NET Core 8 LTS with Pomelo.EntityFrameworkCore.MySql
│       ├── Program.cs                                 # Kestrel pipeline, CORS & EF Core MySQL configuration
│       ├── appsettings.json                           # MySQL connection string & logging configuration
│       ├── Data/InventoryDbContext.cs                 # EF Core DbContext with Fluent API unique indexes
│       ├── Models/Entities.cs                         # StoreLocation, Personnel, InventoryItem, InventoryMovement
│       ├── DTOs/Contracts.cs                          # Request/Response records & DuplicateConflictResponse
│       └── Controllers/
│           ├── DashboardController.cs                 # Dashboard metrics endpoint
│           ├── InventoryController.cs                 # Inventory CRUD, atomic intake, search & history
│           ├── LocationsController.cs                 # Store locations management
│           └── PersonnelController.cs                 # Personnel directory management
├── frontend-vue/                                      # Vue 3 template configuration for IT export
│   └── package.json                                   # Vue 3, Vite, TypeScript, Tailwind
└── src/                                               # Active interactive workstation codebase
    ├── types/inventory.ts                             # Shared TypeScript data contracts
    ├── services/
    │   ├── db.ts                                      # Persistent relational client engine with unique indexes & atomic tx
    │   ├── api.ts                                     # REST client implementing ASP.NET Core contracts
    │   └── scanner.ts                                 # Handheld keyboard scanner listener & barcode classifier
    ├── components/
    │   ├── Navbar.tsx                                 # Industrial top header with plant telemetry & clock
    │   ├── Sidebar.tsx                                # Desktop sidebar & mobile drawer
    │   ├── DuplicateConflictModal.tsx                 # Duplicate resolution dialog
    │   ├── ItemDetailModal.tsx                        # Asset specs & movement audit history
    │   └── CameraScannerModal.tsx                     # Optical barcode scanner with laser reticle & torch
    ├── views/
    │   ├── DashboardView.tsx                          # Live KPIs & storage capacity chart
    │   ├── RegisterView.tsx                           # Dual-barcode registration workstation
    │   ├── InventoryListView.tsx                      # High-density searchable catalog
    │   ├── StoreLocationsView.tsx                     # Locations configuration
    │   ├── PersonnelView.tsx                          # Personnel directory configuration
    │   └── SystemArchitectureView.tsx                 # Architecture audit & IT deployment guide
    ├── App.tsx                                        # Root component & view coordination
    └── main.tsx
```

---

## Installation & Deployment

### 1. Running the Interactive Workstation

The live interactive application runs directly in Node.js 20+ environments:

```bash
# Install dependencies
npm install

# Start local development server on port 3000
npm run dev

# Run static type-checking
npm run lint

# Compile production bundle
npm run build
```

Access the application in your browser at `http://localhost:3000`.

### 2. Running the Production ASP.NET Core API

Prerequisites:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8.0 Server running on port 3306

```bash
cd backend/IndustrialInventory.Api

# Restore NuGet packages
dotnet restore

# Build release binary
dotnet build -c Release

# Configure MySQL connection string in appsettings.json
# "Server=localhost;Port=3306;Database=industrial_inventory;User=inventory_app;Password=YourPassword;"

# Launch Web API service
dotnet run --launch-profile https
```

Swagger API documentation will be available at `https://localhost:7001/swagger`.

### 3. Deploying the MySQL Database

Execute the DDL schema directly on MySQL 8.0+:

```bash
mysql -u root -p < backend/schema.sql
```

Alternatively, apply migrations using the .NET Entity Framework CLI:

```bash
cd backend/IndustrialInventory.Api
dotnet tool install --global dotnet-ef
dotnet ef database update
```

---

## Security & Compliance

- **No Secrets in Frontend**: Database credentials reside exclusively in server-side configuration (`appsettings.json` or environment variables).
- **Relational Integrity**: Deletion of master locations and personnel referenced by inventory items is blocked via foreign key `RESTRICT` rules.
- **Strict HTTPS**: Enforced in production deployments via TLS termination reverse proxies (Nginx / IIS / Envoy).
- **CORS Hardening**: Kestrel API enforces an origin allowlist (`Cors:AllowedOrigins`), rejecting unauthorized external origins.
- **ISA-5.1 & ASME Alignment**: Identifier classification and tracking follow industrial standards for physical serials and instrument tag loops.

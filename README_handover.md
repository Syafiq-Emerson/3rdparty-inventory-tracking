# Industrial Inventory Management System

A web application for registering and tracking industrial inventory,
store locations, personnel, and inventory movements. Inventory records
are identified by a **Serial Number** and a **Tag Number**.

## Technology Stack

-   **Frontend:** React, TypeScript, Vite, Tailwind CSS
-   **Backend:** ASP.NET Core 8 Web API
-   **Database:** MySQL 8.x
-   **Data access:** Entity Framework Core 8 with the Pomelo MySQL
    provider

## Main Features

-   Register inventory with Serial Number, Tag Number, store location,
    and registering personnel.
-   Prevent duplicate Serial Numbers and Tag Numbers.
-   View dashboard totals, recent registrations, and inventory by
    location.
-   Search and filter the inventory catalog.
-   View inventory details and movement history.
-   Manage store locations and personnel.
-   Scan barcodes using a compatible USB/Bluetooth scanner; camera
    scanning is available in the UI where supported by the device and
    browser.

## Repository Layout

``` text
.
├── backend/
│   ├── schema.dev.sql
│   ├── schema.sql
│   └── IndustrialInventory.Api/
│       ├── Controllers/
│       ├── Data/
│       ├── Models/
│       ├── DTOs/
│       ├── Program.cs
│       └── IndustrialInventory.Api.csproj
├── src/
│   ├── components/
│   ├── services/
│   ├── views/
│   ├── types/
│   └── App.tsx
├── package.json
└── README.md
```

## Prerequisites

Install or obtain access to:

-   Git
-   Node.js and npm
-   .NET 8 SDK
-   MySQL Server 8.x
-   MySQL Workbench or the MySQL command-line client

## 1. Clone the Repository

``` bash
git clone <REPOSITORY_URL>
cd 3rdparty-inventory-tracking
```

Replace `<REPOSITORY_URL>` with the approved repository URL.

## 2. Prepare the Database

The application database is named `industrial_inventory`.

Two SQL scripts are provided:

-   **`backend/schema.dev.sql`** --- creates the database and tables
    without sample application records. Use this for a clean development
    setup.
-   **`backend/schema.sql`** --- creates the database and tables and
    inserts sample records. Review the sample data and obtain IT
    approval before using this script in a company environment.

Before importing a script:

1.  Confirm the target MySQL server and environment with IT.
2.  Back up any existing database that could be affected.
3.  Import the selected script into the intended MySQL server using
    MySQL Workbench or the command line.
4.  Create a dedicated application database account with only the
    permissions required by the application.

Example command, run from the repository root:

``` bash
mysql -u <DB_ADMIN_USER> -p < backend/schema.dev.sql
```

Replace `<DB_ADMIN_USER>` with an authorized MySQL administrator
account. Enter the password at the prompt; do not put it in the command.

**Important:** The application does not automatically create or migrate
the database schema at startup. Prepare the schema separately before
starting the API. Do not run either script against an existing company
database without reviewing its effects.

## 3. Configure the Backend

The API reads its database connection string from the
`ConnectionStrings:DefaultConnection` configuration key.

For local development, configure it using .NET User Secrets from the API
project directory:

``` powershell
cd backend/IndustrialInventory.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<MYSQL_HOST>;Port=3306;Database=industrial_inventory;User=<APP_DB_USER>;Password=<APP_DB_PASSWORD>"
```

Replace the placeholders with values supplied or approved by IT. If User
Secrets are already initialized, do not initialize them again.

For a company deployment, use the organization's approved
secret-management or environment-specific configuration process. Do not
commit real credentials to Git or put passwords in this README.

## 4. Start the Backend API

From the repository root:

``` powershell
dotnet run --project .\backend\IndustrialInventory.Api
```

Check the terminal output for the actual API URL and port. The API's
Swagger UI is configured for Development mode; its URL depends on the
active launch settings.

## 5. Start the Frontend

Open a second terminal at the repository root:

``` powershell
npm install
npm run dev
```

Open the URL printed by Vite in the terminal. Confirm that the frontend
API configuration points to the backend URL and that the backend CORS
allowlist permits the frontend origin.

For a production deployment, IT should configure the frontend API
address, hosting, HTTPS, and CORS according to the target environment
rather than relying on local development URLs.

## 6. Verify the Installation

-   [ ] MySQL is running and the `industrial_inventory` schema is
    prepared.
-   [ ] The API starts without database connection errors.
-   [ ] The frontend loads in a browser.
-   [ ] Dashboard data is retrieved from the API.
-   [ ] Inventory can be registered and appears in the inventory
    catalog.
-   [ ] Duplicate identifiers are rejected.
-   [ ] Locations and personnel can be viewed and managed.
-   [ ] Inventory details and movement history can be viewed.
-   [ ] Data remains in the database after restarting the API.

## API Endpoints

  -------------------------------------------------------------------------------
  Method                  Endpoint                        Purpose
  ----------------------- ------------------------------- -----------------------
  GET                     `/api/dashboard/summary`        Dashboard summary

  GET / POST              `/api/inventory`                List inventory /
                                                          register an item

  GET                     `/api/inventory/{id}`           Retrieve an inventory
                                                          item

  GET                     `/api/inventory/search`         Search inventory

  GET                     `/api/inventory/{id}/history`   Retrieve movement
                                                          history

  GET / POST              `/api/locations`                List / create store
                                                          locations

  PUT / PATCH             `/api/locations/{id}`           Update a location /
                                                          change its status

  GET / POST              `/api/personnel`                List / create personnel

  PUT / PATCH             `/api/personnel/{id}`           Update personnel /
                                                          change status
  -------------------------------------------------------------------------------

## Security and Handover Notes

-   **Do not share or commit the MySQL `root` password as part of the
    project handover.** IT should use its approved administrator
    credentials.
-   IT should create or assign an application-specific database account.
    The existing local `inventory_app` password does not need to be
    reused.
-   Share secrets only through an approved secure channel or secret
    manager.
-   Confirm whether any existing inventory data must be migrated; SQL
    scripts and Git source code are not a substitute for a database
    backup or data export.
-   Review sample records in `schema.sql` before use.
-   Confirm the database host, API URL, frontend URL, CORS origins,
    HTTPS setup, backups, and operational ownership with IT.
-   Test the setup on a clean machine or environment before accepting
    the handover.

## Known Setup Notes

-   The backend uses MySQL, not Microsoft SQL Server.
-   The API connection string is read from
    `ConnectionStrings:DefaultConnection`.
-   Database schema setup is a separate step; startup migrations are not
    currently enabled in `Program.cs`.
-   The actual frontend and API URLs should be taken from their runtime
    output or approved deployment configuration, not assumed from this
    README.

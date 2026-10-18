# Industrial Inventory Management System

Web application for registering and tracking industrial assets, store
locations, personnel, and inventory movements.

## Technology

-   Frontend: React, TypeScript, Vite, Tailwind CSS
-   Backend: ASP.NET Core 8 Web API
-   Database: MySQL 8.x
-   Data access: Entity Framework Core 8 with Pomelo MySQL provider

## Prerequisites

Install or obtain access to Git, Node.js and npm, the .NET 8 SDK, MySQL
Server 8.x, and MySQL Workbench or the MySQL command-line client.

## First-time setup

### 1. Clone the repository

``` powershell
git clone <REPOSITORY_URL>
cd 3rdparty-inventory-tracking
```

Replace `<REPOSITORY_URL>` with the approved repository URL.

### 2. Create the database schema

The database is named `industrial_inventory`.

-   `backend/schema.dev.sql`: creates the database and tables without
    sample application records. Use for a clean local setup.
-   `backend/schema.sql`: includes sample records. Review those records
    and obtain approval before using this script in a company
    environment.

Confirm the target server and back up any database that could be
affected before importing a script. Do not run scripts against an
existing company database without review.

From the repository root, import the empty development schema in
PowerShell:

``` powershell
cmd /c "mysql -u root -p < backend\schema.dev.sql"
```

Enter the MySQL root password when prompted. `root` is used for database
setup only, not for the application's normal database connection.

Verify the tables:

``` powershell
mysql -u root -p -D industrial_inventory -e "SHOW TABLES;"
```

Expected tables: `inventory_items`, `inventory_movements`, `personnel`,
and `store_locations`.

If `mysql` is not recognized, use the MySQL command-line client from its
installed `bin` directory or configure that directory in `PATH`.

### 3. Confirm the application database account

The API should connect using a dedicated MySQL account such as
`inventory_app`, not `root`.

Check whether it exists by signing in to MySQL as an administrator and
running:

``` sql
SELECT User, Host
FROM mysql.user
WHERE User = 'inventory_app';
```

If the account is missing, ask the database administrator to create it
and grant only the permissions required on `industrial_inventory`. Do
not grant global administrator privileges to the application account.

### 4. Configure the backend connection string

The API reads `ConnectionStrings:DefaultConnection`. For local
development, configure it with .NET User Secrets.

From the repository root:

``` powershell
cd .\backend\IndustrialInventory.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=industrial_inventory;User=inventory_app;Password=YOUR_INVENTORY_APP_PASSWORD"
```

Replace `YOUR_INVENTORY_APP_PASSWORD` with the actual `inventory_app`
password; do not type the placeholder literally. If
`dotnet user-secrets init` reports that a User Secrets ID already
exists, skip that command and run `dotnet user-secrets set`.

Confirm the setting exists:

``` powershell
dotnet user-secrets list
```

This command displays the secret value. Do not share its output or
include it in screenshots.

**Important:** ASP.NET Core's standard configuration loads User Secrets
in the Development environment, not Production. Confirm that
`Program.cs` uses the standard configuration providers before relying on
this behavior.

### 5. Start the backend API

Stop any older instance before restarting. If port `5000` is already in
use, identify the process:

``` powershell
netstat -ano | findstr :5000
Get-Process -Id <PID>
```

Replace `<PID>` with the process ID shown by `netstat`. Stop it only
after confirming it is the old instance of this application:

``` powershell
Stop-Process -Id <PID>
```

From the repository root, start the backend in Development mode:

``` powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\backend\IndustrialInventory.Api
```

Keep this terminal open. Confirm that output says
`Hosting environment: Development` and shows the API listening URL. The
environment variable applies to this PowerShell session; set it again in
a new session when needed.

### 6. Test the backend before starting the frontend

Open a second PowerShell terminal:

``` powershell
Invoke-RestMethod http://localhost:5000/api/Dashboard/summary
```

Proceed when this returns a dashboard summary. If it returns HTTP 500,
inspect the backend terminal for the underlying exception. If it cannot
connect, confirm the actual API URL and port.

### 7. Start the frontend

Open a third terminal at the repository root:

``` powershell
npm install
npm run dev
```

Open the URL printed by Vite.

If the frontend shows `Failed to fetch`, verify in this order:

1.  The backend is still running.
2.  The direct dashboard API test succeeds.
3.  The frontend API configuration points to the actual backend URL.
4.  The backend CORS allowlist includes the frontend origin.
5.  Frontend and backend use compatible HTTP/HTTPS URLs.

Do not change CORS or HTTPS settings until the failing URL and error are
known.

## Verification checklist

-   [ ] MySQL is running.
-   [ ] The database and four tables exist.
-   [ ] `inventory_app` exists and has the required database
    permissions.
-   [ ] The connection string is stored outside source control.
-   [ ] The backend starts without database errors.
-   [ ] `/api/Dashboard/summary` returns a response.
-   [ ] The frontend loads and retrieves dashboard data.
-   [ ] Inventory registration, search, location/personnel management,
    and movement history work.
-   [ ] Data persists after restarting the API.

## API endpoints

  -------------------------------------------------------------------------------
  Method                  Endpoint                        Purpose
  ----------------------- ------------------------------- -----------------------
  GET                     `/api/dashboard/summary`        Dashboard summary

  GET / POST              `/api/inventory`                List inventory /
                                                          register an item

  GET                     `/api/inventory/{id}`           Retrieve an item

  GET                     `/api/inventory/search`         Search inventory

  GET                     `/api/inventory/{id}/history`   Movement history

  GET / POST              `/api/locations`                List / create locations

  PUT / PATCH             `/api/locations/{id}`           Update a location /
                                                          change status

  GET / POST              `/api/personnel`                List / create personnel

  PUT / PATCH             `/api/personnel/{id}`           Update personnel /
                                                          change status
  -------------------------------------------------------------------------------

## Security and handover

-   Do not put database passwords in this README, source code,
    screenshots, or Git.
-   Do not hand over the MySQL `root` password as a routine project
    credential. IT should use its approved administrator access.
-   IT should create or assign a dedicated application account and
    provide its credentials through an approved secure channel.
-   Git contains source code and schema scripts, not a backup of the
    live database or its records.
-   Review sample records in `schema.sql` before using them.
-   Confirm the approved database host, API/frontend URLs, CORS origins,
    HTTPS, backup process, and operational ownership with IT.
-   Test these instructions on a clean machine or environment before
    handover.

## Notes

-   The backend uses MySQL, not Microsoft SQL Server.
-   Database schema creation is separate; automatic startup migrations
    are not enabled in the current `Program.cs`.
-   Confirm actual ports and URLs from runtime output and project
    configuration. Development URLs are not production URLs.

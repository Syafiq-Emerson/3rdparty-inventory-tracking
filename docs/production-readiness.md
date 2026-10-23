# Production readiness checklist

Copy this list into the release ticket and tick it there, not in this file.

## Before publishing

- [ ] `<Version>` and `<InformationalVersion>` bumped in `backend/IndustrialInventory.Api/IndustrialInventory.Api.csproj`
- [ ] `dotnet test IndustrialInventory.slnx` and `npm test` pass
- [ ] `npm run lint` and `npm run build` pass
- [ ] New migrations reviewed (`dotnet ef migrations script --idempotent` if the DBA wants to see the SQL); they apply when the app starts
- [ ] Users told about the update window (the countdown gives them 30 s)

## Server (first deploy, then check after changes)

- [ ] ASP.NET Core 10 Hosting Bundle installed
- [ ] Site at the root of its binding (HTTPS only needed if the phone camera scanner is used)
- [ ] App pool: No Managed Code, one worker process
- [ ] App pool identity can write to `App_Data`
- [ ] App pool identity has a SQL login on the inventory database (schema rights for the first migration)
- [ ] `ConnectionStrings__InventoryConnection` set as a system variable before the first start, and WAS restarted
- [ ] Admin password set (`dotnet IndustrialInventory.Api.dll set-admin-password` from the site folder)

## Publish

- [ ] `dotnet publish backend/IndustrialInventory.Api -c Release -p:PublishProfile=DeployIIS` finished with "The site is back online"

## After publishing

- [ ] `GET /api/health` returns 200 and `GET /api/health/ready` returns 200
- [ ] `GET /api/system/version` shows the new version
- [ ] The event log has no `Database migration failed` entry
- [ ] Sign in and open every screen. Do not register test items: the ledger is immutable, so they can never be removed
- [ ] Deployed commit recorded in the release ticket

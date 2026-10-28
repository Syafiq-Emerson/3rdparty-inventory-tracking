# Industrial Inventory

Tracks physical instruments (valves, transmitters, meters) by the two barcodes on their label, the
**Serial Number** and the **Tag Number**: registration into a store location, issue for packing, and a
chain-of-custody ledger per item. ASP.NET Core 10 Web API + SQL Server, React client, hosted on IIS on
the internal network.

> **Deviations from the shared baseline**
> - Client is React 19 + TypeScript + Tailwind at the repo root (not Vue/Vuetify in `{app}.client/`).
>   Lint is oxlint + `tsc --noEmit`, because `typescript-eslint` does not support TypeScript 7 yet.
> - Server project is `backend/IndustrialInventory.Api` (not `{App}.Server`). There is no SpaProxy: run the
>   API and Vite separately; Vite proxies `/api`.
> - No What's new / release notes. `/api/system/version` still feeds the version shown in the sidebar.
> - Controllers query EF directly instead of going through per-module services. Their query builders are
>   `internal static` so `QueryTranslationTests` can translate them offline.
> - HTTP-only internal site: no HSTS or HTTPS redirection. The phone-camera scanner needs HTTPS (browsers
>   only allow the camera on secure origins), so on HTTP use a keyboard-wedge scanner or manual entry.
> - Migrations always apply at startup; there is no switch to turn that off.

## Modules

| Route (sidebar) | Audience | Purpose |
|---|---|---|
| Dashboard | Signed-in users | Totals, units per location, recent registrations |
| Register Inventory | Signed-in users | Scan both barcodes (wedge scanner or phone camera), pick location and person, register |
| Issue for Packing | Signed-in users | Scan both barcodes, confirm, mark the item issued |
| Inventory List / Search | Signed-in users | Server-side search by serial or tag, filter by location, item history |
| Store Locations | Signed-in users | Add, edit, deactivate locations |
| Personnel | Signed-in users | Add, edit, deactivate personnel |

Sign-in is a cookie session. There is one seeded account, `admin`; no screen creates more accounts yet.

## API

Every endpoint needs a signed-in session except those marked **anonymous**. Errors are `{ "error": "..." }`.

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/health` | Liveness, no database — **anonymous** |
| GET | `/api/health/ready` | `SELECT 1`, 5 s timeout, 503 when the database is unreachable — **anonymous** |
| GET | `/api/system/version` | `{ version, buildDate }` — **anonymous** |
| GET | `/api/system/deploy-notice` | Update countdown, or 204 — **anonymous** |
| POST | `/api/auth/login` | Sign in; 5 attempts per minute per IP — **anonymous** |
| POST | `/api/auth/logout` | Sign out |
| GET | `/api/auth/me` | Current user, or 401 — **anonymous** |
| POST | `/api/auth/change-password` | Change own password (min. 12 characters) |
| GET | `/api/dashboard/summary` | Dashboard figures |
| GET | `/api/inventory` | Page of items (`?locationId=&page=&pageSize=`) → `{ items, hasMore }` |
| GET | `/api/inventory/{id}` | One item |
| GET | `/api/inventory/search` | Search serial/tag (`?q=&locationId=`), max 50 |
| GET | `/api/inventory/identify` | Item by both barcodes (`?serialNumber=&tagNumber=`) |
| POST | `/api/inventory` | Register item + initial movement; honours `Idempotency-Key` |
| POST | `/api/inventory/issue` | Issue for packing; honours `Idempotency-Key` |
| GET | `/api/inventory/{id}/history` | Movement ledger for an item |
| GET/POST | `/api/locations` | List (`?activeOnly=true`) / create |
| PUT | `/api/locations/{id}` | Update code and name |
| PATCH | `/api/locations/{id}/status` | Activate / deactivate |
| GET/POST | `/api/personnel` | List (`?activeOnly=true`) / create |
| PUT | `/api/personnel/{id}` | Update code and name |
| PATCH | `/api/personnel/{id}/status` | Activate / deactivate |

## Layout

```
├── IndustrialInventory.slnx
├── .config/dotnet-tools.json              pinned dotnet-ef
├── .vscode/                               launch ("Server + Vite"), tasks (build, test, publish, db:*, client:*)
├── docs/production-readiness.md           release checklist
├── backend/
│   ├── IndustrialInventory.Api/
│   │   ├── Program.cs                     composition root, pipeline, startup migration, set-admin-password verb
│   │   ├── Data/                          InventoryDbContext (schema "industrialinventory"), design-time factory
│   │   ├── Migrations/                    the only way the schema changes
│   │   ├── Sql/SqlObjects.cs              CREATE OR ALTER text for triggers, applied by migrations
│   │   ├── Models/                        entities; Models/Audit for AuditEvent and its constants
│   │   ├── DTOs/                          request/response records
│   │   ├── Controllers/                   Auth, Health, DeployNotice, Dashboard, Inventory, Locations, Personnel
│   │   ├── Services/Auth                  cookie sign-in, password hashing, set-admin-password
│   │   ├── Services/Deploy                deploy notice (App_Data/deploy-notice.json)
│   │   ├── Services/Inventory             InventoryRules: paging, LIKE escaping, idempotency key, duplicate body
│   │   ├── Services/Shared                ServiceResult, AuditWriter, IdempotencyGate, AppVersion, exception handler
│   │   └── Properties/PublishProfiles/    DeployIIS.pubxml + app_offline.htm
│   └── IndustrialInventory.Api.Tests/     xUnit, no database (Auth, Audit, Data, Deploy, Inventory, Shared)
└── src/                                   React client
    ├── App.tsx                            boot overlay, auth gate, view switch
    ├── services/api.ts                    fetch wrapper, ApiError, idempotency keys
    ├── services/scanner.ts                keyboard-wedge listener, serial/tag classifier, placeScan rules
    ├── hooks/useAsync.ts                  load-on-mount/key change, reload, stale-response guard
    ├── hooks/useDeployNotice.ts           polls the deploy notice, reloads after an update
    ├── utils/csv.ts, download.ts          CSV export (BOM, formula-injection guard) and file save
    ├── components/                        shell, modals, camera scanner, deploy banner
    └── views/                             one file per screen, lazy-loaded
                                           (*.test.ts files sit next to the code they test)
```

## Configuration

No secrets in `appsettings.json`. Production values are machine-level environment variables; development
uses `dotnet user-secrets`.

| Environment variable | Purpose | Default |
|---|---|---|
| `ConnectionStrings__InventoryConnection` | SQL Server connection string. **Required**; the app will not start without it. | *(empty)* |
| `ASPNETCORE_ENVIRONMENT` | `Development` enables Swagger and user-secrets. | `Production` |
| `VITE_API_TARGET` | Dev only: where Vite proxies `/api`. | `http://localhost:5000` |

## Database objects

Everything lives in the `industrialinventory` schema; migration history is
`industrialinventory.__EFMigrationsHistory`.

| Object | Kind | Purpose |
|---|---|---|
| `StoreLocation` | Table | Locations; `UX_StoreLocation_LocationCode`; soft-deleted via `IsActive` |
| `Personnel` | Table | People who scan; `UX_Personnel_PersonnelCode`; soft-deleted via `IsActive` |
| `InventoryItem` | Table | One row per instrument; `UX_InventoryItem_SerialNumber`, `UX_InventoryItem_TagNumber` |
| `InventoryMovement` | Table | Append-only ledger; `UX_InventoryMovement_IdempotencyKey` (filtered) |
| `AppUser` | Table | Sign-in accounts; seeded `admin` with no password |
| `AuditEvent` | Table | Append-only audit trail (create/update/deactivate/restore/login/post) |
| `CK_InventoryMovement_EventType` | Check | `InitialRegistration`, `IssueForPacking` |
| `CK_AppUser_Role` | Check | `Admin`, `Operator` |
| `TR_InventoryMovement_Immutable` | Trigger | Blocks UPDATE/DELETE on the ledger (THROW 51002) |
| `TR_AuditEvent_Immutable` | Trigger | Blocks UPDATE/DELETE on audit rows (THROW 51000) |

Trigger text lives in `Sql/SqlObjects.cs`. To change one, edit the constant and add a migration that
re-applies it. Because `InventoryMovement` has an INSTEAD OF trigger, its foreign keys use
`ON DELETE NO ACTION`; items and master data are never hard-deleted.

## Development

Prerequisites: .NET 10 SDK, Node `^22.18.0 || >=24.12.0`, SQL Server LocalDB.

```powershell
dotnet tool restore
cd backend\IndustrialInventory.Api
dotnet user-secrets set "ConnectionStrings:InventoryConnection" "Server=(localdb)\MSSQLLocalDB;Database=IndustrialInventory;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run                          # applies migrations, http://localhost:5000, Swagger at /swagger
dotnet run -- set-admin-password    # once per database; input is hidden
```

```powershell
npm install
npm run dev      # http://localhost:3000, proxies /api to :5000
npm run lint     # oxlint + tsc --noEmit
npm test         # vitest
npm run build
dotnet test IndustrialInventory.slnx
```

Or use the VS Code launch configuration **Server + Vite** (F5). It opens one terminal tab for the API and
one for Vite. Run `npm ci` once after cloning, or F5 can't start Vite.

Schema changes: `dotnet ef migrations add <Name>` from `backend\IndustrialInventory.Api`, then `dotnet run`.
To start over locally: `dotnet ef database drop --force`, `dotnet run`, `dotnet run -- set-admin-password`.

## Deployment

### Release flow

1. Bump `<Version>` and `<InformationalVersion>` in `IndustrialInventory.Api.csproj`.
2. `dotnet test IndustrialInventory.slnx`, `npm test`, `npm run lint`, `npm run build`.
3. `dotnet publish backend/IndustrialInventory.Api -c Release -p:PublishProfile=DeployIIS`
   - Builds the client (`npm ci`, `npm run build`) and ships `dist/` as `wwwroot`
     (`-p:SkipClientBuild=true` reuses an existing `dist/`).
   - Writes `App_Data\deploy-notice.json` on the site, so open pages show a 30 s countdown
     (`-p:IndustrialInventoryNoticeSeconds=0` skips it).
   - Copies `app_offline.htm`, waits for `IndustrialInventory.Api.dll` to unlock, copies the files from
     `bin\DeployIIS\` to `\\mynilasdmfgdb01\Automation\IndustrialInventory`, then removes `app_offline.htm`.
   - On failure the site stays on the update page: publish again, or delete `app_offline.htm` on the share.
4. Migrations apply when the app starts. For a DBA to review the SQL first, hand over
   `dotnet ef migrations script --idempotent`.
5. Work through `docs/production-readiness.md` in the release ticket and record the deployed commit.

### One-time server setup

- ASP.NET Core 10 Hosting Bundle; event-log source `IndustrialInventory.Api` registered (or run the first
  start as an admin).
- Site at the root of its binding, since the client uses base path `/`. HTTP is fine for everything except
  the phone camera scanner, which needs an HTTPS binding.
- App pool: No Managed Code; write access to `App_Data` (Data Protection keys and the deploy notice;
  losing the keys signs everyone out; publishing never overwrites `App_Data`). One worker process, since
  the idempotency gate is in memory.
- SQL login for the app pool identity with `db_owner` on the database for the first deploy (it creates
  the `industrialinventory` schema), or have the DBA run the idempotent script.
- Production uses its own new, empty database, never the one used for testing: the ledger and audit
  tables are immutable, so test rows could never be removed. The first start creates the schema and
  seeds only `admin`; enter the real store locations and personnel through the UI.
- `ConnectionStrings__InventoryConnection` as a machine-level (system) variable before the first start, then
  `net stop was /y` and `net start w3svc`.
- First admin password, from the site folder with the same environment:
  `dotnet IndustrialInventory.Api.dll set-admin-password`.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `The database connection string is not set.` | Variable or user-secret missing | Set it (see Configuration) and restart |
| `/api/health/ready` returns 503 | SQL Server unreachable | Start LocalDB (`sqllocaldb start MSSQLLocalDB`) or check the server/login |
| Login says the account has no password | Fresh database | `set-admin-password` |
| Login returns 429 | 5 attempts in a minute from one IP | Wait 60 s |
| `Movement records cannot be changed or deleted.` | Something tried to edit the ledger | By design; post a reversing entry |
| Site stuck on "being updated" | Publish failed after going offline | Publish again, or delete `app_offline.htm` |
| Publish: `did not release 'IndustrialInventory.Api.dll'` | App pool did not stop in 60 s | Recycle the app pool and publish again |

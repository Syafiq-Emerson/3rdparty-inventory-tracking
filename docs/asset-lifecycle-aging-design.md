# Asset Lifecycle & Storage Aging — Architecture Design

**Status:** Proposed; design only
**Scope:** Permanent inventory identity with repeatable storage and issue cycles
**Source of truth:** Checked-out `dev` branch at `7370d7a`; repository documentation and code were inspected. No live database was queried.

## Executive summary

Add a dedicated, append-preserving `StorageCycle` for each uninterrupted period in which an asset is in a store. Keep one `InventoryItem` per physical asset and retain the existing unique Serial Number and Tag Number indexes. A filtered SQL Server unique index permits at most one open cycle per item. The inventory controller’s existing registration, issue, identify, and history routes are reused; add only `POST /api/inventory/return-to-storage`.

The cycle is the source of truth for whether an asset is in storage and for its age. Retain `InventoryItem.IsIssued` and `LocationId` as compatibility/current-state fields for existing queries and clients, but update them atomically with the cycle and reject/reconcile any mismatch. Aging is computed from UTC timestamps at read time; no counter is stored. Each transition writes the cycle, movement ledger row, item state, and audit event in one SQL transaction.

## Verified existing architecture

- Backend: ASP.NET Core 10 Web API, EF Core, SQL Server. Tables use schema `industrialinventory`; EF migrations define schema changes and are applied during app startup.
- Client: React 19 + TypeScript + Tailwind CSS 4, built with Vite and served by the API in IIS. The repository does not use Next.js.
- `InventoryItem` already has unique indexes on `SerialNumber` and `TagNumber`, `CreatedAtUtc`, current `LocationId`, and `IsIssued`. Its registration timestamp is generated from injected `TimeProvider`.
- Registration creates one item, an `INITIAL_REGISTRATION` movement, and an audit row in a transaction. Issue uses a guarded `IsIssued = false` update, creates an `ISSUE_FOR_PACKING` movement, and writes an audit row in a transaction.
- `InventoryMovement` and `AuditEvent` have database triggers preventing UPDATE/DELETE. The movement has a filtered unique index on `IdempotencyKey`; the in-memory gate assumes one IIS worker. Client calls currently generate a new key per invocation.
- Inventory controller actions use authenticated-only `[Authorize]`. `AuthService` issues a `ClaimTypes.Role` claim for the existing `Admin` or `Operator` role, but current inventory actions do not require either role explicitly. The cookie is `HttpOnly`, `SameSite=Lax`, uses `SecurePolicy.SameAsRequest`, and has a 12-hour sliding lifetime. No anti-forgery validation or cookie-principal revalidation was found in the inspected backend source.
- `GET /api/inventory/{id}/history` currently returns only a movement array. `ItemDetailModal` loads it alongside item details and renders the movement ledger.
- `StoreLocation` and `Personnel` are soft-deactivated, but their names/codes can be edited. The movement history currently joins current master-data names.
- The repository has only `docs/production-readiness.md`; no equivalent lifecycle design, migration, API-contract, or test-plan document existed. README states that the production database is intended to be new/empty. This repository inspection does not establish the state of any deployed database.

## Business rules and state model

| State | `InventoryItem.IsIssued` | Storage cycle | Current storage age |
|---|---:|---|---|
| In storage / available | `false` | Exactly one active row (`IssuedAtUtc IS NULL`) | Whole completed 24-hour periods |
| Issued for packing | `true` | No active row; the last cycle is closed | `null` / UI em dash |

### Approved lifecycle authorization matrix (D1 — BOTH)

| Caller | Register Inventory | Issue for Packing | Return to Storage |
|---|---:|---:|---:|
| `Admin` | Allow | Allow | Allow |
| `Operator` | Allow | Allow | Allow |
| Unauthenticated | Deny (`401`) | Deny (`401`) | Deny (`401`) |
| Authenticated without either role | Deny (`403`) | Deny (`403`) | Deny (`403`) |

`AuthService` already issues a `ClaimTypes.Role` claim. Current lifecycle write actions have only authenticated `[Authorize]`; implementation must register one explicit `InventoryLifecycle` policy requiring the existing `Admin` or `Operator` claim and apply it consistently to all three mutation actions. No new role is introduced. Existing inventory read routes remain authenticated as they are today.

1. A successful registration creates one `InventoryItem`, its first cycle, the initial movement, and its audit event atomically. Use one backend UTC instant for `CreatedAtUtc`, first `StoredAtUtc`, and initial `MovedAtUtc`. The transaction’s commit makes the registration and first cycle visible together.
2. Issue requires the exact Serial Number + Tag Number pair and an in-storage item. It closes the active cycle once and stops current aging. Permanent identifiers and completed cycle data remain unchanged.
3. Return requires an issued item, the exact identifier pair, an active location, and active responsible personnel. It creates a new cycle; it never creates or re-identifies an item.
4. **Responsible Personnel** is the `Personnel` selected for the physical operation; **Authenticated Operator** is the signed-in `AppUser` performing it. They are distinct identities. Never infer equality, accept the authenticated identity from the request body, or substitute one for the other.
5. All transition timestamps come from the backend `TimeProvider`. Capture one `operationUtc` per accepted command and use it for the item state change, cycle boundary, and movement. Audit should use the same instant (or explicitly document its independently sampled timestamp).
6. Completed cycle boundaries are permanent. Aging and durations are derived from boundaries, not periodically updated counters.

**Aging:** for one read snapshot `nowUtc`, calculate `floor((nowUtc - StoredAtUtc).Ticks / TimeSpan.TicksPerDay)` for an active cycle. For a closed cycle use `IssuedAtUtc - StoredAtUtc`. Do not use calendar-day `DATEDIFF(day, ...)`, which counts midnight boundaries rather than elapsed 24-hour periods. A future start timestamp is an integrity anomaly; do not silently present a negative age or clamp it without recording the anomaly.

For each cycle, calculate elapsed ticks from its stored start to its issue end, or to the same response `nowUtc` for an active cycle. Floor that cycle’s elapsed ticks to whole 24-hour days, then calculate `TotalStorageDays = SUM(each cycle's durationDays)`. Do not carry fractional days between cycles or sum elapsed ticks before flooring. For example, 12d20h + 25d8h + active 3d6h yields `12 + 25 + 3 = 40` total storage days, not 41.

## Database design

Create `industrialinventory.StorageCycle` with:

| Column | Type / nullability | Purpose |
|---|---|---|
| `Id` | `nvarchar(36)`, PK | GUID string, consistent with existing entities |
| `InventoryItemId` | `nvarchar(36)`, required FK | Permanent asset |
| `LocationId` | `nvarchar(36)`, required FK | Location for this particular storage period |
| `StoredAtUtc` | `datetime2(3)`, required | Start boundary |
| `IssuedAtUtc` | `datetime2(3)`, nullable | End boundary; null means active |
| `StoredByPersonnelId` | `nvarchar(36)`, required FK | Person responsible for placing/returning the asset to storage |
| `IssuedByPersonnelId` | `nvarchar(36)`, nullable FK | Person responsible for closing the cycle |
| `LocationCodeSnapshot`, `LocationNameSnapshot` | bounded strings, required | At-event labels; display only, the FK remains authoritative |
| `StoredByPersonnelCodeSnapshot`, `StoredByPersonnelNameSnapshot` | bounded strings, required | Preserve readable attribution if the master record is renamed |
| `IssuedByPersonnelCodeSnapshot`, `IssuedByPersonnelNameSnapshot` | bounded strings, nullable | Set on closure to preserve at-event attribution |

`StoredByPersonnelId` is preferable to a cycle-level `RegisteredByPersonnelId`: it expresses the responsible person for *each* cycle, including returns. On the first cycle it equals `InventoryItem.RegisteredByPersonnelId`; it can differ on later cycles. `InventoryItem.CreatedAtUtc` remains the registration timestamp and equals the first cycle’s `StoredAtUtc`; do not add a redundant `RegisteredAtUtc`. `IssuedAtUtc` is intentionally duplicated with the closing movement’s timestamp: it is the cycle’s directly queryable end boundary and must equal that movement’s `MovedAtUtc`.

The selected **Responsible Personnel** is represented by `InventoryItem.RegisteredByPersonnelId` for registration, `StorageCycle.StoredByPersonnelId`/`IssuedByPersonnelId` for cycle boundaries, and `InventoryMovement.PerformedByPersonnelId` for the event. The authenticated **Operator** is represented separately by the existing `AuditEvent.UserName` value from the authenticated `ClaimTypes.Name` claim, written by `AuditWriter`; the lifecycle audit snapshot must also include the responsible personnel ID. `UserName` is an audit snapshot, not a personnel FK. Never set it from `performedByPersonnelId` or the UI selection.

Add `InventoryMovement.StorageCycleId` as a nullable FK to `StorageCycle`, with `ON DELETE NO ACTION` and an index. Also add a unique alternate key on `(StorageCycle.Id, StorageCycle.InventoryItemId)` and a composite FK from `(InventoryMovement.StorageCycleId, InventoryMovement.InventoryItemId)` to it. This prevents a movement for one item from pointing to another item’s valid cycle. Lifecycle movements must reference the cycle they open/close: initial registration and return point to the newly opened cycle; issue points to the cycle it closes. Keep the existing movement table and immutable ledger. Add `RETURNED_TO_STORAGE` to the allowed event type constraint. For current lifecycle event types, require a non-null cycle reference; define how future non-lifecycle movement types will be added to that constraint.

### Constraints and indexes

- Preserve `UX_InventoryItem_SerialNumber` and `UX_InventoryItem_TagNumber` unchanged.
- PK on `StorageCycle.Id`; FKs to `InventoryItem`, `StoreLocation`, and `Personnel` for stored/issued actors, all with `ON DELETE NO ACTION`/`RESTRICT`.
- `CK_StorageCycle_IssuePair`: `IssuedAtUtc` and `IssuedByPersonnelId` are either both null or both non-null.
- `CK_StorageCycle_Chronology`: `IssuedAtUtc IS NULL OR IssuedAtUtc >= StoredAtUtc`.
- `UX_StorageCycle_OneActivePerItem`: unique `InventoryItemId` filtered by `[IssuedAtUtc] IS NULL`. This is the final concurrency guard against a second active cycle.
- `IX_StorageCycle_Item_StoredAtUtc`: `(InventoryItemId, StoredAtUtc)` for chronological history; index each FK used for location/personnel queries where not already covered by the composite or filtered index.
- `UQ_StorageCycle_Id_InventoryItemId` alternate key, required as the composite movement FK target.
- `FK_InventoryMovement_StorageCycle_Item` on `(StorageCycleId, InventoryItemId)` and a matching index. Retain the existing item FK, unique identifier indexes, and append-only movement/audit behavior.
- Add filtered unique movement indexes, one per exact event type, allowing at most one `INITIAL_REGISTRATION`, one `RETURNED_TO_STORAGE`, and one `ISSUE_FOR_PACKING` per non-null cycle. The insert-time trigger must also reject a second opener of the other type, so the cycle has at most one opener in total; use SQL Server-supported simple equality filters rather than relying on an `IN` predicate in a filtered index.
- Add an insert-time movement trigger that validates lifecycle event-to-cycle semantics: opening event type/time/actor/destination agree with an active cycle; `INITIAL_REGISTRATION` is used only for the item’s first cycle and `RETURNED_TO_STORAGE` only when an earlier cycle exists; issue type/time/actor/source agree with its closed cycle. The composite FK enforces item ownership. Keep transaction-level checks that every newly committed cycle has its opening event and every closed cycle has its issue event; run a reconciliation check to flag any gap.
- Add a `StorageCycle` database trigger (consistent with the existing SQL-object pattern) that rejects DELETE and all edits to completed rows; permit only the one-way active-to-closed update of the issue timestamp, issue actor, and issue-actor snapshots, with all identity, start, location, and stored-by fields unchanged. Test EF Core SQL Server trigger/output-clause compatibility for the selected trigger; configure the provider accordingly if required.

The filtered unique index enforces “at most one” active row, including racing writers. SQL constraints cannot enforce that `InventoryItem.IsIssued` is the inverse of active-cycle existence across tables. Therefore, cycle presence is canonical for storage/aging; all lifecycle commands must atomically maintain `IsIssued` and current/last `LocationId`. Reads and a reconciliation query must detect mismatches. Do not let alternate write paths change those compatibility fields independently.

SQL Server FKs/checks and the insert trigger can reject malformed references and event rows, but they cannot require a corresponding movement to exist whenever a cycle is inserted/closed at transaction commit. The enforcement boundary for event-pair completeness is the single supported lifecycle command path: it writes the cycle and movement in the same transaction, and any failure rolls back both. Do not expose direct lifecycle-table writes to other runtime services or scripts; DBA maintenance writes must be controlled and audited. Repository deployment guidance currently allows the application pool SQL identity to have `db_owner` for startup migrations, which does not provide strict write isolation. Before production implementation, decide whether to separate migration DDL credentials from the runtime principal; otherwise document that trusted backend code and DBA-only maintenance are the enforcement boundary, and run a scheduled reconciliation check with alerting for missing pairs or state mismatches.

Foreign-key IDs are authoritative; snapshot labels are display-only. Backfill snapshots reflect the labels available at migration time unless the existing audit log proves the historical value. Preserve cycle and movement rows even when referenced locations/personnel are deactivated; never hard-delete referenced master data.

## Transactional workflows

All commands validate identifiers, active master records, and current state on the server. Start one SQL transaction under the configured EF execution strategy. Capture one UTC instant per logical request, and roll back all writes on any failed guard, constraint, movement, or audit write.

### Initial registration

1. Normalize the two identifiers; validate both are non-empty and within the existing 100-character limit.
2. Validate the selected active location and personnel.
3. Create `InventoryItem` with `IsIssued=false`, `LocationId`, original responsible personnel, and `CreatedAtUtc=operationUtc`.
4. Create an active cycle with `StoredAtUtc=operationUtc`, selected location/personnel and snapshots.
5. Add `INITIAL_REGISTRATION` movement with `ToLocationId`, `PerformedByPersonnelId`, `StorageCycleId`, and `MovedAtUtc=operationUtc`.
6. Add the audit event using the authenticated operator from claims and include the separately selected responsible personnel ID, item/cycle IDs, and state change in the audit snapshot.
7. Save and commit. SQL unique indexes arbitrate simultaneous duplicate serial/tag registrations; map the loser to the existing structured duplicate 409.

### Issue for Packing

1. Identify using both trimmed identifiers; validate active responsible personnel. Reject unknown identifiers with 404, inactive personnel with the established 400 shape, and an already issued item with 409.
2. Capture `operationUtc`. Inside the transaction perform a conditional item update where `IsIssued=false`; require exactly one row. Set `IsIssued=true` and `UpdatedAtUtc`.
3. Close the item’s active cycle with a guarded update `WHERE InventoryItemId=@id AND IssuedAtUtc IS NULL`, setting `IssuedAtUtc`, `IssuedByPersonnelId`, and issue snapshots. Require exactly one row; otherwise roll back the item update and report a state conflict/integrity error.
4. Add `ISSUE_FOR_PACKING` movement linked to the closed cycle, with `FromLocationId` equal to the cycle location, `ToLocationId=null`, and the same timestamp/personnel.
5. Add an audit event identifying the authenticated operator and separately recording the responsible personnel, then commit. The conditional item update serializes opposing transitions; the active-cycle filtered unique index is an independent integrity guard.

### Return to Storage

1. Identify using both identifiers; validate that the item is issued, the location exists and is active, and personnel exists and is active. Reject an in-storage item with 409.
2. Capture `operationUtc`. In one transaction conditionally update the item `WHERE IsIssued=true`, setting `IsIssued=false`, its new current `LocationId`, and `UpdatedAtUtc`. Require one row.
3. Create a new active cycle for this item with the selected location/personnel, snapshots, and `StoredAtUtc=operationUtc`. A filtered-index violation or state conflict rolls back the item update.
4. Add `RETURNED_TO_STORAGE` movement linked to the new cycle, `FromLocationId=null`, `ToLocationId` equal to the new location, and the same timestamp/personnel.
5. Add an audit event identifying the authenticated operator and separately recording the responsible personnel, then commit. A duplicate request or concurrent return cannot create a second active cycle.

### Idempotency, retries, and audit consistency

- Continue to accept `Idempotency-Key` for registration and issue; require it for return and keep the existing durable unique movement-key index. Validate keys longer than 64 characters instead of silently truncating them.
- A retry must reuse the same key. The client must retain that key for the submitted operation until success or definitive failure; do not generate a fresh key for a timeout retry.
- Before applying a command, look up a persisted movement by key. Replay only when event type and canonical operation inputs (item identity, personnel, location where applicable) match; return a replay response for that operation. Reusing the key for a different action/body is 409. If a simultaneous cross-process request loses on the unique key, roll back/dispose its transaction, reload the winning movement in a fresh query, compare the inputs, and return the original result with 200 when they match (409 only for mismatched reuse). An in-memory “already processing” response may remain a 409 while the winner is uncommitted; the caller can retry with the same key. The index, not the in-memory gate, is the correctness boundary.
- If an execution-strategy retry or connection failure makes commit outcome ambiguous, check the durable key before retrying the state transition; never create a second logical operation.
- A movement, cycle, item update, and audit row commit or roll back together. The movement’s `StorageCycleId`, type, timestamp, responsible personnel, and from/to location must agree with the cycle boundary. Audit `UserName` identifies the authenticated operator from `AuditContext`; audit snapshots separately include the selected responsible personnel.
- Current `AuditWriter` samples its own timestamp; implementation should pass the command’s `operationUtc` to audit writing (or make timestamp differences explicitly acceptable) so lifecycle event correlation is deterministic.

## API design overview

Reuse `POST /api/inventory` for registration and `POST /api/inventory/issue` for issue, adding cycle writes inside their existing transactions. Keep `GET /api/inventory/identify` for the two-identifier confirmation step. Add one `POST /api/inventory/return-to-storage`. Add `currentAgingDays` (nullable) to inventory list/search/detail responses; issued assets have null. Evolve the existing `GET /api/inventory/{id}/history` into a combined envelope containing the item, cycles, and movements; update the bundled client in the same deployment because the current response is an array.

Full request/response shapes, status codes, and validation are specified in [asset-lifecycle-aging-api-contracts.md](./asset-lifecycle-aging-api-contracts.md).

## Frontend behaviour

- Add a **Current Aging** catalog column. Show `N days` for a stored asset (including `0 days` before its first completed 24 hours) and an em dash for issued assets. Treat `isIssued` as the display state and validate the nullable age agrees with it.
- Change/augment the current Details action to **History** without adding a separate history API. Keep item identifiers and current state at the top of the existing modal; render chronological storage cycles, each location, stored/issued timestamps, actors, whole-day duration, and active marker, followed by movement activity and actors. Show total storage days as the sum of each cycle’s individually floored `durationDays`.
- Add a Return to Storage screen using the existing dual scanner/manual input pattern. Require and visually confirm both identifiers; call the existing identify endpoint and require `isIssued=true`; select active location/personnel using current master-data endpoints; show an explicit confirmation before posting. On 409, re-identify; on ambiguous network timeout, retry with the same idempotency key. Show clear success/error feedback and do not clear inputs on an unconfirmed outcome.
- Label the selected person as **Responsible Personnel**; do not imply it is the signed-in operator. Match current enterprise styling (`#004B87`, slate panels, compact tables, responsive layouts) and provide labels, loading, empty, not-found, stale-state, and failure states. UI validation is not authorization or concurrency control.

## Security, integrity, risks, and alternatives

- **Approved D1 — BOTH roles:** Admin and Operator may each register inventory, issue for packing, and return to storage. Apply one explicit `InventoryLifecycle` authorization policy to all three mutation endpoints, requiring the existing `Admin` or `Operator` role claim. Current actions are authenticated-only; register the policy in authorization configuration and apply it consistently to registration, issue, and return. Keep anonymous access denied (401), return 403 for an authenticated principal lacking either role, preserve existing cookie authentication, and add no roles. Do not make authorization depend on the selected Responsible Personnel.
- Validate active location/personnel and transition state in the API, not only in the UI. Keep database constraints and transactions as the final defense. Keep error responses on the existing `{ "error": "..." }` convention; never leak SQL errors or identifiers beyond current policy.
- **Approved D2 — SEPARATE identities:** Audit the authenticated operator (`AuditEvent.UserName` from claims) and the selected Responsible Personnel (Personnel FKs on item/cycle/movement plus responsible-person ID in audit snapshots) separately. Never infer that they are the same person or let the request body override the authenticated operator.
- **CSRF strategy — implementation requirement:** Use ASP.NET Core antiforgery tokens for unsafe browser API requests. A same-origin `GET /api/auth/csrf-token` obtains a request token from `IAntiforgery.GetAndStoreTokens`; return the request token in a `Cache-Control: no-store` JSON response and store the paired antiforgery cookie. Configure the token header as `X-CSRF-TOKEN`. Apply `AutoValidateAntiforgeryToken` (or an equivalent central MVC authorization filter) to unsafe API actions so missing/invalid tokens are rejected with a generic 400 before the controller action executes. Protect login, logout, change-password, registration, issue, and return; do not exempt a lifecycle mutation.
- The React API client must fetch/refresh the token after page load and after authentication changes, retain the request token in memory only, and attach it in `X-CSRF-TOKEN` to every unsafe same-origin request. Never place it in a URL or logs. Strictly compare `Origin` on unsafe browser requests with deployment-configured exact origins; reject mismatches and missing origins with 403. Do not use wildcard or suffix matching. SameSite and HttpOnly cookie attributes are defense-in-depth, not CSRF controls by themselves.
- **Session revalidation — implementation requirement:** The cookie currently has a 12-hour sliding lifetime, and the role claim is captured at sign-in. No per-request principal revalidation was found. Implement cookie `OnValidatePrincipal` revalidation on every authenticated request against the current `AppUser` identified by `NameIdentifier`: reject and sign out the cookie if the account is missing/inactive, the stored role differs from the cookie role, or the role is not an allowed existing role. Fail closed if authoritative account state cannot be checked; do not continue using a stale principal. A role change requires reauthentication so the next cookie contains the current claim. In the current role-only model, account deactivation and role change are the authorization source of truth. If future per-user grants can be revoked without changing role, add a persisted authorization/security-stamp version and compare it during the same validation.
- `SameSite=Lax` only mitigates some cross-site requests; `HttpOnly` limits script access to the cookie. Neither replaces request-token validation. Deployment must also preserve the exact trusted origin and use HTTPS with secure cookies where the environment supports HTTPS.
- Snapshot labels protect readable history from later master-data renames; trade-off is denormalized display data. Never use snapshots instead of FKs for integrity.
- Alternative: infer all storage periods from movement timestamps only. Rejected because it requires reconstructing active state and cycle joins from event sequences and leaves no direct, enforceable “one active cycle” key.
- Alternative: persist an aging counter or compute it on the workstation. Rejected because counters become stale and workstation clocks are untrusted.
- Alternative: add a separate lifecycle-history route. Rejected absent external-client evidence; evolve the existing history route and deploy API/client together. This response-shape change must be called out for any undocumented consumers.

## Assumptions and unresolved business questions

1. A cycle’s storage location is immutable for that cycle. A move while in storage, if needed, should be separately designed as a movement within the cycle rather than editing its location.
2. `InventoryItem.LocationId` remains the last/current assigned location even while issued; `StorageCycle.LocationId` describes each historical storage interval.
3. Historical master-data snapshots for pre-existing rows may not recover the label as it was at the original event. Confirm whether backfill-time labels plus audit records are acceptable for legacy rows.
4. Production database contents have not been independently queried. Migration preflight and backfill must be validated against a protected production backup before approval.
5. The deployment’s canonical browser origin(s) and HTTPS termination must be confirmed before configuring anti-forgery origin validation. The current source does not establish those deployment values.

The approved day aggregation is **SUM** of individually floored cycle durations; it is not an unresolved business question. The approved authorization scope is both existing roles, and the selected Responsible Personnel remains distinct from the Authenticated Operator.

## Verification gates before implementation/release

Before release, implement and test the anti-forgery token/header flow, strict origin validation, and per-request cookie-principal revalidation. Inspect a production backup; implement schema and API compatibility together; run SQL Server integration tests for constraints, transaction rollback, and concurrency; run existing .NET and client test suites; review the idempotent migration SQL; deploy during the repository’s documented IIS update window; reconcile cycle/item state after backfill and after rollout. No application, schema, or database changes are part of this design task.

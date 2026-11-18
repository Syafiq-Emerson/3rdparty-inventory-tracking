# Asset Lifecycle & Storage Aging — Migration Plan

**Status:** Proposed; no EF migration generated or applied
**Database:** SQL Server, schema `industrialinventory`
**Related design:** [asset-lifecycle-aging-design.md](./asset-lifecycle-aging-design.md), [asset-lifecycle-aging-erd.mmd](./asset-lifecycle-aging-erd.mmd)

## Existing database assessment

### Repository evidence

- EF Core model and the single checked-in `InitialCreate` migration define `InventoryItem`, `InventoryMovement`, `StoreLocation`, `Personnel`, `AppUser`, and `AuditEvent`.
- `InventoryItem` has unique serial/tag indexes, required location and registration-personnel FKs, `IsIssued`, `CreatedAtUtc`, and `UpdatedAtUtc`.
- Current registration writes an initial movement and audit record in a transaction, assigning the same injected backend `TimeProvider` timestamp to item creation and movement. Current issue writes an issue movement and updates `IsIssued` transactionally.
- The movement event check currently allows only `INITIAL_REGISTRATION` and `ISSUE_FOR_PACKING`. Movement idempotency keys have a filtered unique index. Movement and audit tables have immutable triggers.
- README says deployed production is intended to use a new empty database, migrations apply during API startup, and an idempotent SQL script can be generated for DBA review.

### What was not inspected

No live database connection or production data was queried. Repository code/documentation cannot prove the contents, row counts, drift, or backup state of any deployed database. Before approval, inspect a production backup/read-only snapshot and run the preflight queries below. Do not connect this design task to production or alter data.

## Migration sequence

Use the repository’s EF Core migration workflow only after design approval and implementation. Generate SQL with the repository’s pinned tooling, review it with the DBA, and validate on a disposable database before deployment.

### 0. Preflight and backup

Run read-only checks on a protected backup:

1. Count inventory items, movements by event type, audits, active locations/personnel, and `IsIssued` values.
2. Find each item with zero or multiple `INITIAL_REGISTRATION` movements.
3. Compare item `CreatedAtUtc` with its initial movement `MovedAtUtc`; current registration writes the same instant, so unexpected differences require investigation.
4. Find `IsIssued=true` items with zero/multiple issue movements; find `IsIssued=false` items with any issue movement. The current model is one-way (issue only), so these rows are not safely inferable.
5. Check every issue movement time is at/after registration, FK targets exist, identifiers remain unique, and no lifecycle rows have malformed timestamps.
6. Confirm immutable triggers and movement unique-index definitions match the checked-in model.

**Gate:** Missing/duplicate events, inconsistent state, impossible chronology, or unknown event types stop automatic backfill. Resolve them with a reviewed, auditable reconciliation procedure; do not fabricate timestamps or actors.

### 1. Expand schema

In an additive EF migration:

1. Create `StorageCycle` with its PK, required item/location/stored-person FKs, nullable issue timestamp/person FK, snapshots, `datetime2(3)` timestamp precision, and non-cascading delete behavior.
2. Add chronology and issue-pair check constraints.
3. Add filtered unique index on `StorageCycle.InventoryItemId WHERE IssuedAtUtc IS NULL`, the `(InventoryItemId, StoredAtUtc)` history index, and supporting FK indexes.
4. Add nullable `InventoryMovement.StorageCycleId`, the `StorageCycle` alternate key `(Id, InventoryItemId)`, the composite non-cascading FK `(StorageCycleId, InventoryItemId)`, and its supporting index. Keep the existing item FK.
5. Expand the movement event check to include `RETURNED_TO_STORAGE`; add a check that each known lifecycle movement has a cycle link. Add separate filtered unique indexes for each exact lifecycle event type (simple equality predicates supported by SQL Server); the INSERT trigger must prevent two different opening-event types from linking to the same cycle. Keep current identifier indexes unchanged.
6. Add/recreate the storage-cycle immutability trigger after the backfill, permitting only one active-to-closed transition and rejecting delete/edits to completed cycles. Add an INSERT trigger validating lifecycle event type, cycle ownership, boundary timestamp, actor, from/to location, and whether initial registration versus return is appropriate for the cycle sequence. Verify EF Core’s SQL Server trigger/output behavior before applying.

The existing immutable movement trigger blocks UPDATE/DELETE. To backfill `InventoryMovement.StorageCycleId`, the migration must perform a controlled, transactional trigger drop/recreate around the one-time updates, or use a reviewed alternative that preserves immutable history. Run with the API offline/maintenance window while the schema and backfill are in progress. Recreate and verify the trigger before the migration commits or before the API accepts traffic. Audit rows themselves are not rewritten.

### 2. Backfill one cycle per existing item

For each `InventoryItem`, create exactly one historical cycle:

- `StoredAtUtc = InventoryItem.CreatedAtUtc`, after preflight confirms it agrees with the unique initial-registration movement timestamp.
- `LocationId`, `StoredByPersonnelId`, and event snapshots come from the initial-registration movement and referenced master rows; confirm they agree with the item’s original location and registered personnel.
- For an item with `IsIssued=false`, leave `IssuedAtUtc` and `IssuedByPersonnelId` null.
- For an item with `IsIssued=true`, set `IssuedAtUtc` and `IssuedByPersonnelId` from its unique `ISSUE_FOR_PACKING` movement and referenced person; capture its location/person labels. Require `IssuedAtUtc >= StoredAtUtc`.
- Populate the corresponding initial-registration and issue movements’ `StorageCycleId` with the created cycle ID. Never change event type, movement timestamp, actor, or location.

The existing application does not support return events, so the expected legacy history is at most one cycle per item. Do not infer prior unrecorded returns. Snapshot labels for legacy rows are only as historically accurate as available data; master-data audit entries may help but are not guaranteed to reconstruct every past label.

The migration must preserve the existing responsible-person IDs in `InventoryItem` and `InventoryMovement`; use them to populate the first cycle’s responsible attribution. Keep historical `AuditEvent.UserName` values unchanged as the authenticated-operator snapshots. Do not backfill by equating a selected Personnel row to an AppUser.

### 3. Validate before commit/enablement

- Verify `StorageCycle` row count equals `InventoryItem` row count for the one-way legacy model.
- Verify each item has exactly one initial cycle, all lifecycle movement links resolve through the composite `(StorageCycleId, InventoryItemId)` FK, and each cycle has exactly one opening movement and (when closed) one matching issue movement.
- Verify the inverse state invariant: `IsIssued=true` iff no active cycle; `IsIssued=false` iff exactly one active cycle.
- Verify timestamps, actors, location IDs, and snapshot values against the source movements/item.
- Verify both original unique identifier indexes remain enabled and unchanged.
- Verify triggers, checks, filtered indexes, composite ownership FK, and all other FKs exist and reject invalid writes (including a movement pointing at another item’s cycle) in SQL Server integration tests.
- Commit the schema/backfill atomically if supported by the generated migration script and size/lock profile. If the dataset is too large for a safe transaction, use an explicitly designed staged migration with maintenance mode and resumability; do not improvise a partial backfill.

## Deployment and compatibility

- Deploy schema before or atomically with the API binary that requires `StorageCycle`; the repository currently runs pending migrations at application startup before mapping controllers. Follow the established IIS app-offline deployment window and obtain DBA approval of `dotnet ef migrations script --idempotent`.
- The React client is bundled with the API deployment. Evolve the history route response and client together; the old client expects a movement array. Inventory DTO additions are additive, but external API consumers have not been inventoried.
- The new event constraint and nullable `StorageCycleId` allow migration/backfill before enabling return. The application version that accepts `RETURNED_TO_STORAGE` must not run against a database with the old event constraint.
- Keep the existing `IsIssued` and `LocationId` fields and API fields in the first release. They remain compatibility fields maintained in the same transaction, not independent sources of storage state.
- `StorageCycle` and movement growth is one row per storage interval and two movement rows per completed cycle (plus registration/return openers). Retain current indexes, review query plans for history/list endpoints, and avoid unbounded history joins or N+1 requests.
- Aging values and `TotalStorageDays` are computed from UTC boundaries, not persisted or backfilled as counters. Each cycle’s whole-day duration is floored independently; the API total is the sum of those per-cycle integers.
- The approved `Admin`/`Operator` authorization policy is an application authorization change, not a new database role or schema migration. Both roles retain all three lifecycle transitions.
- Antiforgery validation, the token endpoint/header contract, exact-Origin checks, and cookie `OnValidatePrincipal` revalidation are application/client deployment requirements, not lifecycle schema migrations. The current `AppUser` active-state and role fields are sufficient for per-request validation; no security-stamp column is needed for the current role-only model. Deploy the API filter/token endpoint and React token handling together, and block lifecycle writes until both are active.
- The authentication cookie currently has a 12-hour sliding lifetime. Every authenticated request must revalidate current account active-state and role against `AppUser`; role change or deactivation rejects the existing cookie and requires sign-in again. Do not rely on cookie expiration alone. If future per-user grants can change independently of role/active state, design and migrate a persisted authorization/security-stamp version before relying on such grants.
- Responsible Personnel remains the existing Personnel FK identity on inventory/cycle/movement records; the authenticated operator remains the existing `AuditEvent.UserName` snapshot. The migration must not conflate or rewrite either attribution.

## Rollback and recovery

- Prefer application rollback while retaining the additive schema. The old application can continue to read existing item fields and movement rows, but cannot provide return behavior and may display the new event generically. Do not allow an old binary to write lifecycle states it does not understand.
- Do **not** run a destructive `Down` migration that drops populated cycles or `StorageCycleId`; after any return, the new table contains the only record of additional storage periods. Treat the migration as forward-only once lifecycle writes begin.
- If data or migration corruption occurs, stop lifecycle writes, preserve logs/backup, compare against the pre-deploy backup and immutable movement/audit records, then use a reviewed forward repair or approved point-in-time restore. A restore may lose post-backup transactions and requires operational approval.
- On migration failure, startup is expected to fail rather than serve against a partially upgraded model. Verify SQL transaction behavior for DDL, trigger management, and backfill on the actual SQL Server edition/version before production.

## Risks and approval gates

- Unknown deployed data may violate the one-initial-event/one-issue assumptions; resolve with backup-based preflight before migration approval.
- Temporarily disabling/recreating the movement immutable trigger is sensitive. Keep it within the migration transaction/maintenance window, verify its definition afterward, and confirm audit/movement row counts and checksums.
- SQL Server filtered indexes, trigger update rules, and EF Core SQL output behavior require actual SQL Server tests; offline EF tests do not establish this.
- Existing snapshot labels may be incomplete historically. Record the legacy limitation and retain original audit data.
- Per-request account revalidation adds an authoritative database read to authenticated requests and must fail closed if the current account/role cannot be checked. Validate its latency, availability, and failure behavior before release; do not extend stale-cookie authorization during a database outage.
- The antiforgery request token, paired cookie, and exact trusted Origin configuration must be deployed consistently with the React client and actual IIS/proxy origin. A token endpoint or client header without server-side pre-action validation is not sufficient. SameSite and HttpOnly settings alone do not satisfy the CSRF gate.
- Startup-applied migrations require production DBA review and appropriate SQL permissions. Existing release guidance notes a DBA can run an idempotent script; decide whether that or startup migration will execute this change.
- SQL Server constraints/triggers do not defer checks until commit to require both a cycle and its corresponding movement. Event-pair completeness therefore relies on the single lifecycle command transaction plus reconciliation. The existing deployment guidance may grant the app pool `db_owner`; decide whether to separate migration DDL from runtime credentials and do not claim that direct DML is blocked unless permissions actually enforce it. Restrict any maintenance writer to the DBA process and audit it.
- No migration or database operation is authorized by this design request. Obtain approval before generating/applying the eventual migration or running production preflight against live systems.

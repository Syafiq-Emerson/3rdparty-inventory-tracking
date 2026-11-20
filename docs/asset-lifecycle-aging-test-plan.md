# Asset Lifecycle & Storage Aging — Test Plan

**Status:** Proposed verification plan; no application change or test run is part of this design task.
**Related design:** [asset-lifecycle-aging-design.md](./asset-lifecycle-aging-design.md), [asset-lifecycle-aging-api-contracts.md](./asset-lifecycle-aging-api-contracts.md), [asset-lifecycle-aging-migration-plan.md](./asset-lifecycle-aging-migration-plan.md)

## Test environments and conventions

- Preserve the current offline unit-test approach for pure rules/projections where possible. The existing .NET tests use xUnit and an offline EF model; no SQL-backed integration suite was found.
- Use a disposable SQL Server database for migration, constraints, trigger, transaction, and concurrency tests. Never run mutation scenarios against production.
- Use a fake `TimeProvider` for deterministic boundary checks. Integration tests must set the server-side injected clock, not the test workstation’s current time.
- Give each parallel test isolated data and unique identifier values. Verify durable rows after each response, including failure paths.
- Run all four verification groups before release: focused unit tests, disposable SQL Server integration tests, existing .NET suite, and existing client lint/tests/build.

## Functional tests

### Registration

1. Register a new pair with active Responsible Personnel. Assert one `InventoryItem`, one active `StorageCycle`, one `INITIAL_REGISTRATION` movement linked to the cycle, and one audit event commit together.
2. Assert exactly one item exists and its Serial Number and Tag Number equal the submitted normalized pair. `CreatedAtUtc`, first `StoredAtUtc`, and initial movement `MovedAtUtc` equal the injected UTC operation instant.
3. Authenticate as AppUser A and select Responsible Personnel B. Assert item/cycle/movement store B’s Personnel ID, audit `UserName` stores A’s authenticated username, and the audit snapshot contains B’s Personnel ID. They are never substituted or inferred to match.
4. Assert active cycle has no issue timestamp/actor, captures the location/responsible-person snapshots, and age is `0` at registration.
5. Assert inactive or unknown location/personnel is rejected and creates no item, cycle, movement, or audit row.
6. Submit a duplicate serial only, duplicate tag only, and both duplicate identifiers. Assert existing duplicate conflict shape and no extra rows.

### Issue for Packing

1. Issue an in-storage item using both identifiers and active Responsible Personnel. Assert one active cycle becomes closed exactly once; `IsIssued=true`; current/last `LocationId` remains; exactly one linked `ISSUE_FOR_PACKING` movement and audit event exist.
2. Assert cycle `IssuedAtUtc`, movement `MovedAtUtc`, `IssuedByPersonnelId`, and movement `PerformedByPersonnelId` agree; active aging becomes null.
3. Authenticate as AppUser A and select Responsible Personnel B. Assert the movement/cycle records identify B and the audit `UserName` identifies A; the request cannot override A’s identity.
4. Assert unknown pair is 404; swapped/mismatched identifier pair never identifies an item; already-issued item returns 409; inactive/unknown personnel is rejected.
5. Inject a failure after the item guard, after cycle close, and before audit save/commit. Each failure leaves no partial state, movement, or audit record.

### Return to Storage

1. Return an issued item with both exact identifiers, active location, and active Responsible Personnel. Assert the same item row/IDs remain; a new cycle is active; current `LocationId` changes; `IsIssued=false`; one linked `RETURNED_TO_STORAGE` movement and audit event exist.
2. Assert new cycle `StoredAtUtc` equals movement timestamp and injected UTC instant, with location/actor snapshots; prior cycle boundaries and snapshot values do not change.
3. Authenticate as AppUser A and select Responsible Personnel B. Assert movement/cycle attribution identifies B and audit `UserName` identifies A.
4. Assert active item return is 409; unknown or mismatched identifiers are 404; invalid/inactive location/personnel is rejected; none creates a cycle or movement.
5. Deactivate the previous location/personnel after a completed cycle. Assert history remains queryable; new return requires an active location and active personnel.
6. Inject failures after item update, cycle insert, movement insert, and before audit/commit. Assert transaction rollback restores issued item and creates no open cycle or partial ledger/audit rows.

## Lifecycle transition tests

- Exercise register → issue → return → issue → return. Assert one item ID and permanent identifiers throughout, exactly two completed cycles plus one active cycle after the final return, no gaps/overlaps, and event actors/locations correspond to each cycle.
- Assert issue is allowed only from storage and return only from issued state. Never allow two active cycles or an open cycle for an issued asset.
- Assert closing a cycle twice or changing a completed cycle’s start, location, stored actor, snapshots, or issue boundary is rejected by database enforcement.
- Attempt deleting any storage cycle and updating/deleting any movement or audit row. Assert database append/history protections reject the operation.
- Change a location/personnel display name after a cycle closes. Assert the cycle’s snapshots remain unchanged while FKs still point to the current master rows.

## Aging calculation tests

For an active cycle and for a closed cycle, test duration boundaries using exact injected instants:

| Elapsed time | Expected whole days |
|---:|---:|
| 0 | 0 |
| 23:59:59.999 | 0 |
| 24:00:00.000 | 1 |
| 47:59:59.999 | 1 |
| 48:00:00.000 | 2 |

- Test a start before/after UTC midnight with less than 24 elapsed hours; output remains 0.
- Test UTC offset inputs normalized to UTC and millisecond persistence precision.
- Assert issued assets have `currentAgingDays=null`; they do not retain the last cycle’s age as current aging.
- Assert each historical cycle duration uses its own start/end; active-cycle duration uses one `nowUtc` per response.
- Assert each cycle is floored independently, then total is the sum of those integer cycle durations; fractional hours never carry across cycle boundaries.
- Worked case: Cycle 1 is 12d20h (12 days), Cycle 2 is 25d8h (25 days), and the active Cycle 3 is 3d6h (3 days); assert `totalStorageDays=40`, not 41.
- Test boundary aggregation across cycles: two separate 23:59:59.999 cycles contribute 0 + 0 = 0 days, not one carried day; a 24:00:00 cycle contributes 1 day.
- Test future `StoredAtUtc` as an integrity anomaly; ensure it is not shown as a negative age or silently converted to a plausible value.
- Verify API emits UTC timestamps with `Z` after a SQL Server `datetime2(3)` write/read round trip when EF materializes `DateTimeKind.Unspecified`; no browser/workstation clock participates in the calculation.

## Concurrency tests (SQL Server required)

1. Send simultaneous issue commands for one active item with different idempotency keys. Exactly one succeeds; the other is a 409/state conflict. Exactly one cycle closes and one issue movement/audit record is committed.
2. Send simultaneous returns for one issued item with different keys. Exactly one succeeds; the other conflicts. Exactly one active cycle and one return movement/audit event exist.
3. Race issue against return for the same item. The final state is one of the serializable legal outcomes; there is no state with `IsIssued` inconsistent with active-cycle count, no lost movement, and no duplicate open cycle.
4. Race registrations with same serial but different tags, then same tag but different serials. SQL unique indexes permit one winner only.
5. Race separate items through cycle creation and verify the filtered index does not block unrelated assets.
6. Run using SQL Server isolation/locking configuration matching production; do not assume an offline EF provider reproduces filtered indexes or lock semantics.

## Idempotency tests

- Retry each transition after a simulated lost response with the same key. Assert one logical transition and return the original event result.
- Repeat the same key concurrently from separate requests and verify the database unique index, not only the in-memory gate, prevents duplicate movement/cycles.
- Race the same key through separate workers so both requests pass the initial lookup. Verify the loser rolls back, reloads the winner outside the failed transaction, validates the canonical request, and returns 200 with the original event result. A mismatched reuse returns 409; a same-process in-progress response may be 409 until the caller retries the same key after commit.
- Reuse a key for a different event type, item, identifier pair, location, or personnel; assert 409 and no mutation.
- Verify a key is not truncated silently: >64 characters is a 400 validation error.
- Reuse an old key after later lifecycle transitions; assert replay returns the original operation’s cycle/movement and does not report a misleading current-state snapshot.
- Verify key lookup/replay for registration, issue, and return after application restart (durable database record remains).
- Verify client timeout retries reuse the same key and editing the submitted payload causes a new operation/key.

## Data integrity and migration tests

- Verify filtered unique active-cycle index allows multiple completed cycles and one active cycle but rejects a second active row.
- Attempt to link an item A movement to item B’s cycle; verify the composite `(StorageCycleId, InventoryItemId)` FK rejects it.
- Verify chronology and issue-pair check constraints; all FKs use no-cascade deletion; FK indexes exist.
- Verify movement event constraint accepts `INITIAL_REGISTRATION`, `ISSUE_FOR_PACKING`, and `RETURNED_TO_STORAGE`, rejects unknown values, and lifecycle movements require valid cycle links.
- Verify per-event-type filtered unique movement indexes and the movement INSERT trigger reject duplicate opening events (including one of each opening type), duplicate issues, wrong registration-versus-return sequence, and wrong actor/timestamp/location for the referenced cycle.
- Verify lifecycle command transactions cannot commit a cycle without its opener or a closed cycle without its issue movement. Separately seed a controlled inconsistent fixture as DBA and verify the reconciliation check detects and alerts on missing pairs; do not imply SQL Server constraints alone enforce deferred pair completeness.
- Verify cycle trigger allows only active-to-completed closure, rejects edits to completed rows and all cycle deletes; test EF Core insert/update SQL against SQL Server with trigger enabled.
- Apply migration to an empty DB and to a disposable representative copy with in-storage/issued records. Validate counts and consistency queries before/after.
- Migration preflight must detect missing/duplicate initial movements, mismatch between item creation and registration movement times, issue/status mismatch, multiple issue events, missing FKs, orphan movement rows, and timestamps before cycle start. Verify the migration stops or routes to explicit reconciliation rather than inventing data.
- Verify backfilled cycle values and movement `StorageCycleId` links; compare per-item before/after item, movement, and audit counts.
- Verify rerunning an idempotent deployment script does not duplicate cycles or alter immutable ledger values.

## API, authorization, and UI tests

- Verify the explicit `InventoryLifecycle` role policy on each of `POST /api/inventory`, `POST /api/inventory/issue`, and `POST /api/inventory/return-to-storage`: Admin and Operator can each perform all three; unauthenticated requests are 401; an authenticated caller without either role is 403. Confirm no new roles and no endpoint is accidentally anonymous or missing the policy.
- For registration, issue, and return, verify the request’s Responsible Personnel remains independent from the authenticated user: movement/cycle FKs identify the selected person, `AuditEvent.UserName` identifies the authenticated AppUser, and audit snapshots retain the Responsible Personnel ID. Do not populate authenticated identity from request data.
- For each of registration, issue, and return, submit a valid anti-forgery request token in `X-CSRF-TOKEN` with an allowed Origin; assert the request reaches the action and behaves normally.
- For each lifecycle mutation, omit the token, then submit an invalid token. Assert a generic 400 is returned before the controller action executes and that no inventory, cycle, movement, or audit state changes.
- Assert an exact allowed Origin succeeds with a valid token; a mismatched or missing Origin is rejected before the action (403). Verify no wildcard or suffix origin match is accepted.
- Verify `GET /api/auth/csrf-token` sets the antiforgery cookie and returns a request token with `Cache-Control: no-store`; a token obtained before login is refreshed after login. Assert the React API client keeps the token in memory, attaches it only to unsafe same-origin requests, and does not place it in a URL, persistent storage, or logs.
- Assess cookie-authenticated `POST /api/auth/logout` and `POST /api/auth/change-password`: valid token/Origin succeeds; missing or invalid token and disallowed Origin are rejected before action execution and cause no sign-out/password-change side effect. Verify `POST /api/auth/login` also requires a token and is usable with the pre-auth token bootstrap.
- Verify cookie `OnValidatePrincipal` rechecks the persisted account on every authenticated request. Deactivate an account and change its stored role after issuing a cookie; the old cookie must be rejected/signed out before a protected action executes. A new login must receive the current role claim. Verify unsupported/mismatched claims are denied and account-state lookup failure does not accept a stale cookie.
- Verify an unauthenticated request is 401. Separately test the `InventoryLifecycle` authorization policy with an authenticated principal lacking either allowed role and assert 403; do not add a persisted role to test the latter. A stale/invalidated cookie rejected during session revalidation is unauthenticated and returns 401.
- Assert API status/body cases (201 first success, 200 replay, 400 validation, 404 unknown pair, 409 stale state/duplicate/idempotency conflict, 401/403) and existing `{error}` compatibility.
- Verify inventory list, search, item detail, and dashboard projections report active age correctly without N+1 queries and return null for issued items.
- Verify the evolved history envelope contains one item, all cycles chronologically, and movements chronologically; each lifecycle movement’s `storageCycleId` resolves to the correct cycle.
- Client: age column shows whole days / em dash, history displays active and completed cycles and movement actors, return flow requires both matching identifiers and explicit confirmation, stale status requires re-identification, and timeout retry preserves the idempotency key.
- Verify loading, empty, error, keyboard-wedge/manual scanner, responsive layout, and accessible labels using existing client test conventions.

## Regression suite and release evidence

After implementation, run the focused changed tests first, then repository baseline commands:

```powershell
dotnet test IndustrialInventory.slnx
npm test
npm run lint
npm run build
```

Also run disposable SQL Server integration/migration tests; the existing repository test command alone cannot prove SQL Server filtered-index, trigger, rollback, or concurrency behavior. Record actual results and the reviewed idempotent migration script in the release ticket. Do not register test rows in production; repository documentation says ledger rows are immutable.

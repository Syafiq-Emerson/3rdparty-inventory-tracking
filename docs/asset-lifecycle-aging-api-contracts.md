# Asset Lifecycle & Storage Aging — API Contracts

**Status:** Proposed; no endpoint or DTO changes made
**Related design:** [asset-lifecycle-aging-design.md](./asset-lifecycle-aging-design.md)

## Conventions

- Existing base path is `/api`; inventory routes are under `InventoryController` with `[Authorize]`.
- Preserve same-origin cookie authentication and established `{ "error": "..." }` errors. Do not mark lifecycle routes anonymous; the safe antiforgery-token bootstrap endpoint is intentionally anonymous and grants no authenticated access.
- The application has `Admin` and `Operator` roles. Current inventory actions use authenticated-only `[Authorize]`; the approved lifecycle contract requires an explicit shared role policy.
- Use the existing `Idempotency-Key` header and durable movement-ledger unique index. Require the caller to reuse the key for retries of the same logical operation.
- Every unsafe browser API request requires a valid ASP.NET Core antiforgery request token in `X-CSRF-TOKEN` and an exact allowed `Origin`. The server validates both before the controller action executes. `SameSite=Lax` and `HttpOnly` are defense-in-depth only.
- Validate/trim identifiers at the API boundary, require the exact matching pair, and validate master records and state server-side.
- All timestamps are UTC. SQL Server `datetime2(3)` carries no offset, and an EF-materialized `DateTime` may have `Kind=Unspecified`. At the DTO boundary, treat persisted values as UTC by invariant (apply `DateTime.SpecifyKind(value, DateTimeKind.Utc)`, never local-time conversion) and serialize UTC `DateTime` values as ISO-8601 ending in `Z`; if `DateTimeOffset` DTOs are used, configure an explicit serializer that emits `Z` for zero offset. Existing and new timestamp fields must be tested after a SQL Server write/read round trip.
- Current .NET response records serialize camelCase. Names below are JSON camelCase examples.

## CSRF and cookie-session security

Use ASP.NET Core `IAntiforgery` with the configured request header `X-CSRF-TOKEN`. Provide `GET /api/auth/csrf-token` with `[AllowAnonymous]` to call `GetAndStoreTokens`, set the paired antiforgery cookie, and return the request token as JSON with `Cache-Control: no-store`. The token endpoint is safe to call before sign-in; the token is not an authentication credential.

Apply `AutoValidateAntiforgeryToken` or an equivalent central MVC authorization filter to unsafe API actions, including login, logout, change-password, lifecycle registration, issue, and return. The pre-auth token endpoint allows login to obtain a token without an authenticated session. Reject a missing or invalid token with a generic `400` before controller action execution and without state changes. Strictly compare `Origin` against exact deployment-configured scheme/host/port values; reject mismatches or missing origins with `403`. Never use wildcard or suffix matching, and do not skip token/origin checks based on user-agent or client-supplied headers.

The React API client fetches the token after page load and after login/logout or other authentication changes, keeps the request token in memory only, and attaches it to every unsafe same-origin request. It must not put the token in URLs, persistent browser storage, or logs. Refresh after login because the anonymous token may not match the authenticated principal. The frontend and API filter must be deployed together.

Cookie tickets currently live for 12 hours with sliding expiration. This is not a revocation mechanism. Implementation must revalidate the cookie principal against the current `AppUser` on every authenticated request; reject/sign out cookies for missing or inactive users, changed role claims, or unsupported roles. A changed role requires reauthentication. The current model has role and active-state authorization only; any future per-user privilege revocation must update state checked by revalidation (and use a persisted authorization/security-stamp version if it is not represented by role or active state).

## Lifecycle authorization matrix

| Caller | Register Inventory | Issue for Packing | Return to Storage |
|---|---:|---:|---:|
| `Admin` | Allow | Allow | Allow |
| `Operator` | Allow | Allow | Allow |
| Unauthenticated | Deny (`401`) | Deny (`401`) | Deny (`401`) |
| Authenticated without either approved role | Deny (`403`) | Deny (`403`) | Deny (`403`) |

Use one explicit `InventoryLifecycle` policy requiring the existing `Admin` or `Operator` role claim, and apply it at the lifecycle write-action level to `POST /api/inventory`, `POST /api/inventory/issue`, and `POST /api/inventory/return-to-storage`. `AuthService` already places `ClaimTypes.Role` in the cookie principal; add the policy to authorization configuration and apply it consistently. Do not introduce roles or authorize based on the selected personnel. Existing authenticated-only authorization remains on read routes. Invalidated/deactivated sessions receive `401`; an authenticated principal without either allowed role receives `403`.

## Separate identities in requests and audit

The request’s personnel ID identifies **Responsible Personnel**: `scanByPersonnelId` for registration and `performedByPersonnelId` for issue/return. These IDs are validated against active `Personnel` rows and stored in item/cycle/movement records as applicable. They are not the authenticated account.

The **Authenticated Operator** is established only by the signed-in cookie principal. Do not accept an operator username or ID in the request body. `AuditWriter` records the authenticated `ClaimTypes.Name` value in `AuditEvent.UserName`; the lifecycle audit snapshot separately records the responsible personnel ID. Never set `UserName` from the selected personnel or infer that the identities match. The existing audit schema has no `AppUser` FK; `UserName` is the audit identity snapshot.

## Existing route integration

### `POST /api/inventory` — initial registration

Keep the current request and `Idempotency-Key` behavior. Require the antiforgery header and valid Origin. In the same transaction, create the item, first active storage cycle, initial movement, and audit row. Do not accept client timestamps.
`scanByPersonnelId` is the Responsible Personnel for registration; the authenticated operator comes from the cookie and is written separately to `AuditEvent.UserName`.

**Request**

```json
{
  "serialNumber": "SN-10429",
  "tagNumber": "TAG-00814",
  "storeLocationId": "loc-guid",
  "scanByPersonnelId": "person-guid"
}
```

**First success:** `201 Created` (existing route behavior). Extend the existing item response additively:

```json
{
  "id": "item-guid",
  "serialNumber": "SN-10429",
  "tagNumber": "TAG-00814",
  "locationId": "loc-guid",
  "locationCode": "WH-A",
  "locationName": "Warehouse A",
  "registeredByPersonnelId": "person-guid",
  "registeredByPersonnelCode": "P-104",
  "registeredByPersonnelName": "Operator One",
  "isIssued": false,
  "currentStorageCycleId": "cycle-guid",
  "currentAgingDays": 0,
  "createdAt": "2026-11-15T13:40:00.000Z",
  "updatedAt": "2026-11-15T13:40:00.000Z"
}
```

`currentAgingDays` is dynamically calculated at response time and may be 0 immediately after commit. `currentStorageCycleId` identifies the active cycle. A matching idempotency replay returns `200 OK`; it must not create another item/cycle/movement. If two workers race with the same key, the request that loses the unique-key insert rolls back, reloads the committed movement outside the failed transaction, compares event and canonical request values, and returns `200` with the original result when they match. A mismatched reuse is `409`. A same-process request may receive an in-progress `409` before the winner commits; retry the same key. Existing duplicate serial/tag conflict behavior remains `409` with `errorCode: "DUPLICATE_IDENTIFIER"`.

### `POST /api/inventory/issue` — issue for packing

Keep current request fields and route. Require the antiforgery header and valid Origin, exact serial/tag, active Responsible Personnel, and one active cycle. `performedByPersonnelId` records the physical responsible person; the authenticated operator is derived from the cookie. Close the cycle in the same transaction as the item state, movement, and audit writes.

**Request**

```json
{
  "serialNumber": "SN-10429",
  "tagNumber": "TAG-00814",
  "performedByPersonnelId": "person-guid"
}
```

**First success:** preserve existing `201 Created`; add the closed cycle to the response:

```json
{
  "item": {
    "id": "item-guid",
    "serialNumber": "SN-10429",
    "tagNumber": "TAG-00814",
    "locationId": "loc-guid",
    "locationCode": "WH-A",
    "locationName": "Warehouse A",
    "isIssued": true
  },
  "storageCycle": {
    "id": "cycle-guid",
    "storedAtUtc": "2026-11-01T10:00:00.000Z",
    "issuedAtUtc": "2026-11-15T13:40:00.000Z",
    "durationDays": 14,
    "isActive": false
  },
  "movement": {
    "id": "movement-guid",
    "inventoryItemId": "item-guid",
    "storageCycleId": "cycle-guid",
    "fromLocationId": "loc-guid",
    "fromLocationName": "Warehouse A",
    "toLocationId": null,
    "toLocationName": null,
    "performedByPersonnelId": "person-guid",
    "performedByPersonnelName": "Operator One",
    "eventType": "ISSUE_FOR_PACKING",
    "movedAt": "2026-11-15T13:40:00.000Z"
  }
}
```

Issued item has no `currentAgingDays`; inventory response uses `null`. Existing same-key replay is `200 OK` and returns the event result; do not return a newly mutated current state as if it were the original operation.

### `GET /api/inventory/identify`

Reuse the current query contract for the operator confirmation step:

`GET /api/inventory/identify?serialNumber=SN-10429&tagNumber=TAG-00814`

Both query parameters are mandatory; comparison is exact after trimming. Return the current item identity/status (`200`), `400` for missing/oversize input, or `404` when the exact pair is unknown. A frontend must re-identify before retrying after a state conflict.

### Inventory list/search/detail

Add `currentStorageCycleId` and `currentAgingDays: number | null` to the shared item response used by:

- `GET /api/inventory`
- `GET /api/inventory/search`
- `GET /api/inventory/{id}`
- dashboard `recentlyRegisteredInventory`

For an in-storage item, return active cycle ID and whole elapsed 24-hour days using a single backend `nowUtc` per response. For issued items return both nullable current fields as `null`. Do not add a new aging endpoint and do not persist or accept an aging counter.

## New route

### `POST /api/inventory/return-to-storage`

**Authorization:** Require the shared `InventoryLifecycle` policy: either `Admin` or `Operator`, consistent with registration and issue. No new role is introduced.
**Idempotency:** Required `Idempotency-Key` header, maximum 64 characters; caller retries with the same key.
**Request:** Client must not send timestamps or item IDs as a substitute for the identifier pair.
**CSRF:** Require the shared antiforgery-token and exact-Origin validation described above. Do not release this route unless both checks run before its action.

```json
{
  "serialNumber": "SN-10429",
  "tagNumber": "TAG-00814",
  "storeLocationId": "loc-b-guid",
  "performedByPersonnelId": "person-guid"
}
```

Validate exact pair, active/issued state, active destination location, and active Responsible Personnel. `performedByPersonnelId` records that selected person, not the authenticated operator. A retry/replay requires matching event type and canonical request values. A key reused with another operation or payload is a conflict.

**First success:** `201 Created`:

```json
{
  "item": {
    "id": "item-guid",
    "serialNumber": "SN-10429",
    "tagNumber": "TAG-00814",
    "locationId": "loc-b-guid",
    "locationCode": "WH-B",
    "locationName": "Warehouse B",
    "isIssued": false,
    "currentStorageCycleId": "cycle-new-guid",
    "currentAgingDays": 0
  },
  "storageCycle": {
    "id": "cycle-new-guid",
    "locationId": "loc-b-guid",
    "locationCodeSnapshot": "WH-B",
    "locationNameSnapshot": "Warehouse B",
    "storedAtUtc": "2026-11-15T14:00:00.000Z",
    "issuedAtUtc": null,
    "storedByPersonnelId": "person-guid",
    "storedByPersonnelCodeSnapshot": "P-104",
    "storedByPersonnelNameSnapshot": "Operator One",
    "issuedByPersonnelId": null,
    "durationDays": 0,
    "isActive": true
  },
  "movement": {
    "id": "movement-new-guid",
    "inventoryItemId": "item-guid",
    "storageCycleId": "cycle-new-guid",
    "fromLocationId": null,
    "fromLocationName": null,
    "toLocationId": "loc-b-guid",
    "toLocationName": "Warehouse B",
    "performedByPersonnelId": "person-guid",
    "performedByPersonnelName": "Operator One",
    "eventType": "RETURNED_TO_STORAGE",
    "movedAt": "2026-11-15T14:00:00.000Z"
  }
}
```

Matching replay: `200 OK`, without another cycle/movement. If a cross-worker race loses on the unique index, roll back and reload the winning movement before returning the original result. This response represents the original operation’s result; clients should refresh current item state after a later transition.

## Combined lifecycle history

### `GET /api/inventory/{id}/history`

Reuse the route; change its response from the current `InventoryMovement[]` to the envelope below and update the bundled client in the same deployment. This is a response-shape break for any external consumers; no such consumer was found in the repository, so inventory them before release. Do not add a parallel history route unless a compatibility consumer is discovered.

```json
{
  "item": {
    "id": "item-guid",
    "serialNumber": "SN-10429",
    "tagNumber": "TAG-00814",
    "isIssued": false,
    "locationId": "loc-b-guid",
    "locationCode": "WH-B",
    "locationName": "Warehouse B",
    "currentAgingDays": 0
  },
  "storageCycles": [
    {
      "id": "cycle-old-guid",
      "locationId": "loc-a-guid",
      "locationCodeSnapshot": "WH-A",
      "locationNameSnapshot": "Warehouse A",
      "storedAtUtc": "2026-11-01T10:00:00.000Z",
      "issuedAtUtc": "2026-11-15T13:40:00.000Z",
      "storedByPersonnelId": "person-a-guid",
      "storedByPersonnelNameSnapshot": "Operator One",
      "issuedByPersonnelId": "person-b-guid",
      "issuedByPersonnelNameSnapshot": "Operator Two",
      "durationDays": 14,
      "isActive": false
    },
    {
      "id": "cycle-new-guid",
      "locationId": "loc-b-guid",
      "locationCodeSnapshot": "WH-B",
      "locationNameSnapshot": "Warehouse B",
      "storedAtUtc": "2026-11-15T14:00:00.000Z",
      "issuedAtUtc": null,
      "storedByPersonnelId": "person-guid",
      "storedByPersonnelNameSnapshot": "Operator One",
      "issuedByPersonnelId": null,
      "issuedByPersonnelNameSnapshot": null,
      "durationDays": 0,
      "isActive": true
    }
  ],
  "movements": [
    {
      "id": "movement-guid",
      "inventoryItemId": "item-guid",
      "storageCycleId": "cycle-old-guid",
      "fromLocationId": "loc-a-guid",
      "fromLocationName": "Warehouse A",
      "toLocationId": null,
      "toLocationName": null,
      "performedByPersonnelId": "person-b-guid",
      "performedByPersonnelName": "Operator Two",
      "eventType": "ISSUE_FOR_PACKING",
      "movedAt": "2026-11-15T13:40:00.000Z"
    },
    {
      "id": "movement-new-guid",
      "inventoryItemId": "item-guid",
      "storageCycleId": "cycle-new-guid",
      "fromLocationId": null,
      "fromLocationName": null,
      "toLocationId": "loc-b-guid",
      "toLocationName": "Warehouse B",
      "performedByPersonnelId": "person-guid",
      "performedByPersonnelName": "Operator One",
      "eventType": "RETURNED_TO_STORAGE",
      "movedAt": "2026-11-15T14:00:00.000Z"
    }
  ],
  "totalStorageDays": 14
}
```

Sort cycles and movements chronologically (ascending for the history presentation; UI may visually group newest first if ordering is explicit). For each cycle, `durationDays = FLOOR(elapsed cycle duration / 24 hours)`; an active cycle uses the response’s backend `nowUtc`. `totalStorageDays = SUM(storageCycles[].durationDays)`. Do not sum elapsed ticks before flooring or carry fractional days between cycles. Example: 12d20h, 25d8h, and active 3d6h produce 12, 25, and 3 days respectively, for `totalStorageDays: 40` (not 41). For an issued item, `currentAgingDays` is null, but the closed cycle durations and total remain available. History loads in the existing item-detail modal without extra per-cycle requests.

## Validation and error handling

| Status | Meaning |
|---|---|
| `201` | New registration, issue, or return committed |
| `200` | Idempotent replay of the matching committed operation |
| `400` | Missing/oversize identifier, malformed payload, inactive/unknown location or personnel, or missing/invalid antiforgery token (`CSRF_VALIDATION_FAILED`); established `{ "error": "..." }` shape |
| `401` | No signed-in cookie session |
| `403` | Signed-in user denied by authorization policy, or request Origin is not allowed |
| `404` | No item matches both identifiers (or item ID is unknown on history/detail routes) |
| `409` | Duplicate serial/tag, invalid current state, concurrent transition, idempotency key reused for different inputs, or database one-active-cycle guard |
| `500` | Unexpected/inconsistent persisted state or unhandled failure; existing error handler returns generic error and trace ID, not SQL details |

Never downgrade a missing active cycle for a supposedly available item into a successful issue, or permit a supposedly issued item with an active cycle to return. Surface the inconsistency for operational investigation. Roll back the entire transition before returning an error.

## Audit and movement semantics

- Initial storage: `INITIAL_REGISTRATION`, `FromLocationId=null`, `ToLocationId=storage location`, links new cycle.
- Issue: `ISSUE_FOR_PACKING`, `FromLocationId=cycle location`, `ToLocationId=null`, links cycle being closed.
- Return: `RETURNED_TO_STORAGE`, `FromLocationId=null`, `ToLocationId=new location`, links new cycle.
- All movement rows preserve the Responsible Personnel FK (`PerformedByPersonnelId`); cycles preserve the responsible person for storage and issue boundaries. Audit rows preserve the Authenticated Operator username from cookie claims in `UserName` and include the separately selected responsible-person/cycle/item IDs in lifecycle snapshots. The response/request contract never treats these identities as interchangeable.
- A composite FK from `(InventoryMovement.StorageCycleId, InventoryMovement.InventoryItemId)` to `(StorageCycle.Id, StorageCycle.InventoryItemId)` prevents cross-item cycle references. Filtered unique movement indexes and an INSERT trigger enforce the opening/closing event’s cycle, timestamp, actor, and location mapping. SQL Server does not defer a constraint to require both rows at commit, so pair completeness relies on the single lifecycle command transaction; restrict other runtime writers and reconcile/alert for missing event pairs. The repository’s current `db_owner` deployment option does not itself enforce that writer boundary.

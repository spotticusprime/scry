# API reference

Scry exposes a JSON REST API. All endpoints are under `/api`. Requests and responses use `application/json` with camelCase field names.

## Common patterns

- **Workspace isolation:** probe, alert, and result endpoints are nested under `/api/workspaces/{workspaceId}`. Requests only see data belonging to the specified workspace.
- **Partial updates:** `PUT` requests accept all nullable fields. Omitting a field (or passing `null`) leaves the existing value unchanged.
- **Soft deletes:** deleting a probe sets `enabled = false`; the probe's history is preserved.
- **GUIDs:** all IDs are UUIDs (`Guid`), returned as lowercase hyphenated strings.
- **Timestamps:** all `*At` fields are ISO 8601 UTC strings.

---

## Health

### `GET /healthz`

Returns the service status and current UTC time. Does not require a workspace.

**Response 200**
```json
{
  "status": "ok",
  "utc": "2026-04-22T16:00:00.000Z"
}
```

---

## Workspaces

### `GET /api/workspaces`

List all workspaces ordered by name.

**Response 200**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Production",
    "description": "Production services",
    "createdAt": "2026-04-01T00:00:00.000Z",
    "updatedAt": "2026-04-01T00:00:00.000Z"
  }
]
```

### `GET /api/workspaces/{id}`

Get a single workspace.

**Response 200** — workspace object (same shape as above)
**Response 404** — workspace not found

### `POST /api/workspaces`

Create a workspace.

**Request body**
```json
{
  "name": "Production",
  "description": "Optional description"
}
```

**Response 201** — created workspace object with `Location` header

### `PUT /api/workspaces/{id}`

Update a workspace. Both fields are required (not partial).

**Request body**
```json
{
  "name": "Production",
  "description": "Updated description"
}
```

**Response 200** — updated workspace object
**Response 404** — not found

### `DELETE /api/workspaces/{id}`

Delete a workspace and all its associated data.

**Response 204**
**Response 404** — not found

---

## Probes

All probe endpoints are scoped to a workspace: `/api/workspaces/{workspaceId}/probes`.

### `GET /api/workspaces/{workspaceId}/probes`

List all probes in the workspace, ordered by name.

**Response 200**
```json
[
  {
    "id": "...",
    "workspaceId": "...",
    "name": "My API",
    "kind": "http",
    "definition": "url: https://api.example.com/health",
    "interval": "00:05:00",
    "enabled": true,
    "createdAt": "...",
    "updatedAt": "..."
  }
]
```

### `GET /api/workspaces/{workspaceId}/probes/{id}`

Get a single probe.

**Response 200** — probe object
**Response 404** — not found

### `POST /api/workspaces/{workspaceId}/probes`

Create a probe and seed its first job run.

**Request body**
```json
{
  "name": "My API",
  "kind": "http",
  "definition": "url: https://api.example.com/health\nexpected_status: 200",
  "interval": "00:05:00"
}
```

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `name` | string | yes | — | Display name |
| `kind` | string | yes | — | `http`, `json_http`, `tcp`, `dns`, `tls` — see [probe docs](probes.md) |
| `definition` | string | yes | — | YAML config for the probe kind |
| `interval` | TimeSpan | no | `00:05:00` | How often to run, e.g. `00:01:00` |

**Response 201** — created probe object

### `PUT /api/workspaces/{workspaceId}/probes/{id}`

Partial update. Any field may be omitted to keep the current value.

**Request body**
```json
{
  "name": null,
  "definition": "url: https://api.example.com/health\nexpected_status: 200\nbody_contains: ok",
  "interval": null,
  "enabled": true
}
```

**Response 200** — updated probe object
**Response 404** — not found

### `DELETE /api/workspaces/{workspaceId}/probes/{id}`

Disables the probe (sets `enabled = false`). Probe history is preserved. The recurring job loop stops after the current in-flight run completes.

**Response 204**
**Response 404** — not found

---

## Alert rules

All alert rule endpoints are scoped to a workspace: `/api/workspaces/{workspaceId}/alerts`.

### `GET /api/workspaces/{workspaceId}/alerts`

List all alert rules, ordered by name.

**Response 200**
```json
[
  {
    "id": "...",
    "workspaceId": "...",
    "name": "API down",
    "expression": "Crit,Error",
    "severity": "Critical",
    "enabled": true,
    "probeIdFilter": null,
    "notifierConfig": "{\"kind\":\"webhook\",\"url\":\"https://hooks.example.com/alert\"}",
    "createdAt": "...",
    "updatedAt": "..."
  }
]
```

### `GET /api/workspaces/{workspaceId}/alerts/{id}`

Get a single alert rule.

**Response 200** — alert rule object
**Response 404** — not found

### `POST /api/workspaces/{workspaceId}/alerts`

Create an alert rule.

**Request body**
```json
{
  "name": "API down",
  "expression": "Crit,Error",
  "severity": "Critical",
  "probeIdFilter": null,
  "notifierConfig": "{\"kind\":\"webhook\",\"url\":\"https://hooks.example.com/alert\",\"method\":\"POST\",\"headers\":{\"Authorization\":\"Bearer token\"}}"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | Display name |
| `expression` | string | yes | Comma-separated `ProbeOutcome` values that trigger the alert: `Ok`, `Warn`, `Crit`, `Error` |
| `severity` | string | yes | `Info`, `Warning`, `Critical` |
| `probeIdFilter` | GUID or null | no | When set, rule only applies to this probe |
| `notifierConfig` | JSON string or null | no | Notifier configuration (see [Notifiers](#notifiers)) |

**Response 201** — created alert rule object
**Response 400** — invalid severity value

### `PUT /api/workspaces/{workspaceId}/alerts/{id}`

Partial update.

**Request body**
```json
{
  "name": null,
  "expression": "Warn,Crit",
  "severity": null,
  "enabled": false,
  "notifierConfig": null
}
```

**Response 200** — updated alert rule object
**Response 400** — invalid severity value
**Response 404** — not found

### `DELETE /api/workspaces/{workspaceId}/alerts/{id}`

Delete an alert rule. Associated `AlertEvent` records are also deleted.

**Response 204**
**Response 404** — not found

### `GET /api/workspaces/{workspaceId}/alerts/{id}/events`

Alert event history for a rule (last 100 events, newest first).

**Response 200**
```json
[
  {
    "id": "...",
    "fingerprint": "{ruleId}:{probeId}",
    "state": "Firing",
    "severity": "Critical",
    "summary": null,
    "openedAt": "...",
    "resolvedAt": null,
    "lastNotifiedAt": "..."
  }
]
```

| `state` | Meaning |
|---------|---------|
| `Firing` | Condition is currently active |
| `Resolved` | Condition cleared; `resolvedAt` is set |

---

## Results

All result endpoints are scoped to a workspace: `/api/workspaces/{workspaceId}/results`.

### `GET /api/workspaces/{workspaceId}/results/latest`

Returns the most recent result for each probe in the workspace.

**Response 200**
```json
[
  {
    "id": "...",
    "workspaceId": "...",
    "probeId": "...",
    "outcome": "Ok",
    "message": "200 OK",
    "durationMs": 42,
    "startedAt": "...",
    "completedAt": "...",
    "attributes": "{\"subject\":\"CN=example.com\"}"
  }
]
```

### `GET /api/workspaces/{workspaceId}/results/{probeId}?limit=50`

Result history for a specific probe, newest first.

| Query param | Default | Range | Notes |
|-------------|---------|-------|-------|
| `limit` | `50` | 1–500 | Number of results to return |

**Response 200** — array of result objects (same shape as above)

---

## Notifiers

A notifier is configured by setting `notifierConfig` on an alert rule to a JSON string containing a `kind` field and kind-specific properties.

### Webhook notifier (`kind: "webhook"`)

Posts a JSON payload to a URL when an alert fires or re-fires (after the 5-minute cooldown).

**`notifierConfig` format**
```json
{
  "kind": "webhook",
  "url": "https://hooks.example.com/alert",
  "method": "POST",
  "headers": {
    "Authorization": "Bearer my-token",
    "X-Custom-Header": "value"
  }
}
```

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `url` | string | required | Webhook destination URL |
| `method` | string | `POST` | HTTP method |
| `headers` | object | `{}` | Additional request headers |

**Payload sent to webhook**
```json
{
  "alertRuleId": "...",
  "alertName": "API down",
  "severity": "Critical",
  "state": "Firing",
  "probeId": "...",
  "workspaceId": "...",
  "outcome": "Crit",
  "message": "503 Service Unavailable",
  "completedAt": "2026-04-22T16:00:00.000Z"
}
```

Non-2xx webhook responses are logged as warnings but do not block probe execution. Failed deliveries do not retry.

---

## Alert evaluation behavior

- Evaluation runs after every probe result is persisted.
- A rule fires when the probe outcome matches any value in `expression`.
- An `AlertEvent` with `state = Firing` is created on the first match.
- Repeat notifications are sent after a 5-minute cooldown (`LastNotifiedAt + 5 min < now`).
- When the condition clears (outcome no longer in `expression`), the event transitions to `state = Resolved` and `resolvedAt` is set.
- Evaluation is best-effort: a notifier failure never discards the probe result.

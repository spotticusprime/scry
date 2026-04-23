# Probe configuration reference

A probe's `definition` field is a YAML string. Field names use snake_case. All durations are `TimeSpan` strings in `hh:mm:ss` format (e.g. `00:00:30` for 30 seconds).

## Probe outcomes

Every probe run produces one of these outcomes:

| Outcome | Meaning |
|---------|---------|
| `Ok` | All conditions passed |
| `Warn` | Partial or advisory failure (e.g. cert expiring, unexpected DNS address) |
| `Crit` | Hard failure threshold exceeded |
| `Error` | Probe configuration error or unhandled exception |
| `Timeout` | Probe exceeded its configured timeout |

---

## `http`

Checks that an HTTP/HTTPS endpoint responds with an acceptable status code and optionally that the response body contains a given string.

```yaml
url: https://example.com/health       # required
method: GET                           # default: GET
timeout: 00:00:30                     # default: 30 s
expected_status: 200                  # default: any 2xx
body_contains: "ok"                   # optional; substring match
headers:
  Authorization: "Bearer token123"    # optional; arbitrary request headers
```

**Outcomes:**
- `Ok` — status code matches (or is 2xx if `expected_status` is omitted), body contains match passes
- `Crit` — status code mismatch or body substring not found
- `Timeout` — connection or response exceeded `timeout`
- `Error` — invalid URL, DNS failure, or unhandled network error

---

## `json_http`

Like `http` but additionally extracts a value from the JSON response body using a dot-notation path and compares it to an expected value.

```yaml
url: https://api.example.com/status   # required
method: GET                           # default: GET
timeout: 00:00:30                     # default: 30 s
expected_status: 200                  # default: any 2xx
json_path: data.health                # dot-notation path into the response JSON
expected_value: "healthy"             # expected string value at that path
headers:
  X-Api-Key: "secret"
```

**Path examples:**
- `status` — top-level field `{"status":"ok"}`
- `data.health` — nested `{"data":{"health":"healthy"}}`
- `items.0.name` — first element of an array `{"items":[{"name":"foo"}]}`

**Outcomes:**
- `Ok` — status code matches, path resolves, value matches `expected_value` (or `expected_value` is omitted)
- `Crit` — status code mismatch, path not found, or value mismatch
- `Timeout` / `Error` — same as `http`

---

## `tcp`

Opens a TCP connection to verify a port is reachable. Does not send or receive data.

```yaml
host: db.internal.example.com        # required; hostname or IP
port: 5432                           # required
timeout: 00:00:10                    # default: 10 s
```

**Outcomes:**
- `Ok` — TCP handshake completed within `timeout`
- `Crit` — connection refused or host unreachable
- `Timeout` — connection did not complete within `timeout`

---

## `dns`

Resolves a hostname and optionally verifies that a specific address appears in the result set.

```yaml
host: example.com                    # required
timeout: 00:00:10                    # default: 10 s
expected_address: 93.184.216.34      # optional; IPv4 or IPv6
```

**Outcomes:**
- `Ok` — hostname resolved (and `expected_address` is in the result set, if specified)
- `Warn` — hostname resolved but `expected_address` is absent. `Warn` rather than `Crit` because the host still resolves — the address difference may be a legitimate DNS change (failover, CDN shift).
- `Crit` — hostname did not resolve at all
- `Timeout` — resolution did not complete within `timeout`

---

## `tls`

Connects to a host over TLS, retrieves the server certificate, and checks its expiry.

```yaml
host: example.com                    # required
port: 443                            # default: 443
timeout: 00:00:10                    # default: 10 s
warn_days: 30                        # default: 30; warn when cert expires within this many days
crit_days: 7                         # default: 7; crit when cert expires within this many days
validate_remote_certificate: false   # default: false; set true to enforce full chain trust
```

**`validate_remote_certificate`:** Defaults to `false` so that self-signed or internal PKI certificates can be monitored for expiry without failing chain validation. Set to `true` in strict PKI environments where you want full chain trust enforced.

**Outcomes:**
- `Ok` — certificate is valid and expires more than `warn_days` from now
- `Warn` — certificate expires within `warn_days` (but not within `crit_days`)
- `Crit` — certificate is expired or expires within `crit_days`
- `Error` — TLS handshake failed (invalid cert with `validate_remote_certificate: true`, wrong host, etc.)
- `Timeout` — connection did not complete within `timeout`

**Certificate metadata** stored in `ProbeResult.Attributes`:
- `subject` — certificate subject (CN)
- `issuer` — issuing CA
- `expires_at` — ISO 8601 expiry timestamp
- `days_remaining` — integer days until expiry

---

## Interval

The probe `interval` is set on the probe itself (not in the `definition` YAML) and controls how frequently Scry re-runs the probe. It is a `TimeSpan` string, e.g.:

| Value | Meaning |
|-------|---------|
| `00:01:00` | Every minute |
| `00:05:00` | Every 5 minutes (default) |
| `00:30:00` | Every 30 minutes |
| `01:00:00` | Every hour |

The minimum practical interval is a few seconds; very short intervals will increase database write volume significantly.

---

## Disabling a probe

Setting `enabled: false` via `PUT /api/workspaces/{wsId}/probes/{id}` (or `DELETE`, which sets enabled to false) stops the recurring job loop. The probe is not deleted and its history is preserved.

To restart a disabled probe, set `enabled: true` — Scry will enqueue a new initial job automatically on the next `PUT`.

> **Note:** In the current implementation, re-enabling a probe via PUT does not automatically re-seed the job loop. You must POST a new probe or use the API to trigger a new initial job. This is a known gap tracked for a future release.

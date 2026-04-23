# Scry

Self-hosted asset inventory and monitoring. Define probes that check your services over HTTP, TCP, DNS, TLS, or JSON APIs — Scry runs them on a schedule, stores the results, and fires alerts when conditions are met.

> Early development. APIs and configuration formats are not yet stable.

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
git clone https://github.com/spotticusprime/scry
cd scry
dotnet run --project src/Scry.Host
```

The database is created automatically at `%APPDATA%/scry/scry.db` (Windows) or `~/.config/scry/scry.db` (Linux/macOS). For development the path defaults to `scry-dev.db` in the project directory via `appsettings.Development.json`.

The API listens on `http://localhost:5000` by default.

```bash
# Health check
curl http://localhost:5000/healthz
```

See [docs/deployment.md](docs/deployment.md) for Docker, systemd, and cloud deployment guides.

## How it works

1. **Create a workspace** — logical grouping for probes and alert rules.
2. **Add probes** — each probe has a `kind` and a YAML `definition` that configures what to check and how often.
3. **Add alert rules** — rules evaluate probe outcomes and fire webhooks when conditions are met.

Probes run on a background job queue. Each completed probe run persists a `ProbeResult` and immediately enqueues the next run at `now + interval`.

## API overview

All routes are under `/api`. Full reference: [docs/api.md](docs/api.md).

| Method | Path | Description |
|--------|------|-------------|
| GET/POST | `/api/workspaces` | List or create workspaces |
| GET/PUT/DELETE | `/api/workspaces/{id}` | Get, update, or delete a workspace |
| GET/POST | `/api/workspaces/{wsId}/probes` | List or create probes |
| GET/PUT/DELETE | `/api/workspaces/{wsId}/probes/{id}` | Get, update, or delete a probe |
| GET/POST | `/api/workspaces/{wsId}/alerts` | List or create alert rules |
| GET/PUT/DELETE | `/api/workspaces/{wsId}/alerts/{id}` | Get, update, or delete an alert rule |
| GET | `/api/workspaces/{wsId}/alerts/{id}/events` | Alert event history |
| GET | `/api/workspaces/{wsId}/results/latest` | Latest result per probe |
| GET | `/api/workspaces/{wsId}/results/{probeId}` | Result history for a probe |

## Probe kinds

| Kind | What it checks |
|------|----------------|
| `http` | HTTP/HTTPS endpoint — status code and optional body substring |
| `http_json` | HTTP endpoint — status code and a value at a JSON path |
| `tcp` | TCP port reachability |
| `dns` | DNS resolution, optional expected address |
| `tls` | TLS certificate validity and days-to-expiry |

Full configuration reference: [docs/probes.md](docs/probes.md).

### Example: HTTP probe

```bash
curl -X POST http://localhost:5000/api/workspaces/{wsId}/probes \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "My API",
    "kind": "http",
    "definition": "url: https://api.example.com/health\nexpected_status: 200",
    "interval": "00:05:00"
  }'
```

### Example: TLS probe

```bash
curl -X POST http://localhost:5000/api/workspaces/{wsId}/probes \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "example.com cert",
    "kind": "tls",
    "definition": "host: example.com\nwarn_days: 30\ncrit_days: 7"
  }'
```

## Alert rules

Alert rules evaluate probe outcomes and fire notifications. The `expression` field is a comma-separated list of `ProbeOutcome` values (`Ok`, `Warn`, `Crit`, `Error`) that trigger the alert.

```bash
curl -X POST http://localhost:5000/api/workspaces/{wsId}/alerts \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "API down",
    "expression": "Crit,Error",
    "severity": "Critical",
    "notifierConfig": "{\"kind\":\"webhook\",\"url\":\"https://hooks.example.com/alert\",\"method\":\"POST\"}"
  }'
```

## Project layout

```
src/
  Scry.Core/        Domain models (Probe, ProbeResult, AlertRule, AlertEvent, …)
  Scry.Data/        EF Core DbContext, migrations, configurations
  Scry.Runner/      Background job queue and dispatcher
  Scry.Probes/      Probe executors (http, tcp, dns, tls, http_json) and alert evaluation
  Scry.Api/         Minimal API endpoint definitions
  Scry.Host/        Entry point — wires everything together, runs migrations
tests/
  Scry.Data.Tests/
  Scry.Probes.Tests/
docs/
  deployment.md     Local, Docker, systemd, cloud deployment
  api.md            Full REST API reference
  probes.md         Probe configuration reference
  security.md       Security considerations and hardening guide
```

## Documentation

- [Deployment guide](docs/deployment.md)
- [API reference](docs/api.md)
- [Probe configuration](docs/probes.md)
- [Security guide](docs/security.md)

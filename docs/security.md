# Security guide

This document covers the current security posture of Scry, known gaps, and hardening recommendations for production deployments.

> **Summary:** Scry has **no built-in authentication or authorization**. Every API endpoint is open to anyone who can reach the port. Do not expose Scry directly to the internet. Always deploy behind a firewall, VPN, or authenticating reverse proxy.

---

## Current security posture

### No authentication

The REST API has no authentication layer. Any client that can reach the Scry port can:

- Read all workspace data, probe configurations, and result history
- Create, modify, or delete probes and alert rules
- Trigger configuration changes that affect which external hosts are probed

**Risk:** High if the port is exposed to untrusted networks.

**Mitigation:** Run Scry on a private network (localhost, VPN, or internal VLAN) and use an authenticating reverse proxy (see [API key auth with nginx](#api-key-auth-with-nginx) below).

### No authorization / multi-tenancy

Workspace isolation is enforced at the database query level (global query filters on `WorkspaceId`), but there is no access control that limits which workspaces an API client can access. An authenticated client can read and modify any workspace.

### SSRF via HTTP probes and webhooks

Two features send outbound HTTP requests to URLs provided by API clients:

1. **HTTP / JSON HTTP probes** — the `url` field in the probe definition is fetched by the server.
2. **Webhook notifiers** — the `url` field in `notifierConfig` is POSTed to when an alert fires.

An attacker with API access can direct Scry to fetch any URL reachable from the server — including internal services, cloud metadata endpoints (e.g. `http://169.254.169.254/latest/meta-data/`), or private network hosts.

**Mitigation:**
- Restrict who can call the API (authentication).
- Consider adding an allowlist of permitted target domains/CIDRs for probes and webhooks.
- On cloud VMs: use IMDSv2 (AWS) or equivalent metadata endpoint protection.

### SSRF via DNS probes

DNS probes resolve arbitrary hostnames. This is lower risk than HTTP (no HTTP response body is returned to the API) but allows internal hostname enumeration from the Scry server's network perspective.

### Probe YAML definition injection

The `definition` field is parsed as YAML server-side. The YAML parser used (`YamlDotNet`) is not known to have arbitrary code execution issues, but malformed input could trigger parse errors that surface in API responses or logs. Input is not sanitized before parsing.

### Secrets in probe definitions and notifier configs

Probe `definition` fields and `notifierConfig` JSON often contain sensitive values (API keys, bearer tokens). These are stored in plaintext in the SQLite database.

**Mitigation:**
- Restrict database file permissions (`chmod 600`).
- Encrypt the database at rest using filesystem-level encryption (LUKS, dm-crypt, BitLocker) or SQLite encryption extensions.
- Back up the database securely; treat backups as sensitive.

### No rate limiting

The API has no rate limiting. A client can make unlimited requests, including high-frequency POSTs that create many probes and jobs. This could exhaust disk space (probe results) or CPU (parallel probe execution).

### No input length limits

Probe `definition`, alert `expression`, and `notifierConfig` have no enforced maximum length. Very large values are accepted and stored.

### TLS certificate validation off by default

`tls` probes default to `validate_remote_certificate: false`. This is intentional (self-signed certs should be monitorable) but means the TLS probe does not detect man-in-the-middle attacks against the probed host.

---

## Hardening checklist

### Network exposure

- [ ] Scry is bound to `127.0.0.1` or a private/VPN interface, not `0.0.0.0`
- [ ] No firewall rule allows direct access to the Scry port from untrusted networks
- [ ] TLS is terminated at a reverse proxy (nginx, Caddy) — not exposed as plain HTTP
- [ ] The server hosting Scry has outbound firewall rules limiting which hosts probes can reach

### Authentication

- [ ] An authenticating reverse proxy (or API gateway) sits in front of Scry
- [ ] API keys or mTLS credentials are rotated periodically

### Database

- [ ] Database file has `chmod 600` and is owned by the `scry` service user
- [ ] Database directory is not world-readable
- [ ] Automated off-host backups are configured (e.g. Litestream → S3)
- [ ] Backup files are encrypted at rest

### Systemd service hardening

If running as a systemd service, verify these options are set in the unit file:

```ini
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/var/lib/scry
ProtectHome=true
CapabilityBoundingSet=
```

See [deployment.md](deployment.md#linux-systemd-service) for the full unit file.

### Docker

- [ ] Container runs as a non-root user (`USER scry` in Dockerfile)
- [ ] Port is bound to `127.0.0.1` only: `-p 127.0.0.1:8080:8080`
- [ ] Volume is mounted with minimal permissions

### Cloud metadata endpoints

On cloud VMs (AWS EC2, GCP, Azure):

- [ ] IMDSv2 is enforced (AWS) to prevent unauthenticated metadata access
- [ ] Outbound probe targets are restricted to known domains/CIDRs where possible

---

## API key auth with nginx

Until Scry has built-in authentication, the simplest production-grade approach is to require a static API key at the nginx layer.

> **Security note:** The key value below is stored in the nginx config file in plaintext. Anyone who can read `/etc/nginx/sites-available/scry` will see it. Restrict file permissions (`chmod 640`, owned by `root:www-data`) and rotate the key periodically. nginx `if` string comparison is also not constant-time, so this is suitable for internal/VPN deployments but not as a substitute for properly authenticated access in multi-tenant or internet-facing contexts.

```nginx
server {
    # ... TLS config as in deployment.md ...

    location / {
        # Require X-Api-Key header
        if ($http_x_api_key != "your-secret-key-here") {
            return 401 '{"error":"unauthorized"}';
        }

        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Clients include the key in every request:

```bash
curl -H "X-Api-Key: your-secret-key-here" https://scry.example.com/api/workspaces
```

For stronger authentication, consider:
- **Authelia / Authentik** — full OIDC provider that can protect nginx locations
- **Caddy + forward auth** — simpler setup with Let's Encrypt built in
- **Tailscale / Wireguard** — VPN-based access; no public exposure at all

---

## Known gaps (future work)

These are not bugs — they are intentional deferrals for an early-stage codebase. They should be addressed before using Scry in a multi-user or internet-facing context.

| Gap | Risk | Tracking |
|-----|------|----------|
| No API authentication | High — unauthenticated API access | Planned |
| No per-workspace ACL | Medium — any authed user sees all workspaces | Planned |
| No SSRF protection on probe URLs | Medium — requires API access to exploit | Future |
| Plaintext secrets in DB | Medium — requires DB access to exploit | Future |
| No rate limiting | Low — requires API access | Future |
| No input length validation | Low — requires API access | Future |
| No audit log | Low | Future |

---

## Reporting security issues

Please report security vulnerabilities privately by emailing the maintainers rather than opening a public GitHub issue.

# Deployment guide

- [Local development](#local-development)
- [Production build (bare metal / VPS)](#production-build-bare-metal--vps)
- [Linux systemd service](#linux-systemd-service)
- [Docker](#docker)
- [Docker Compose](#docker-compose)
- [Reverse proxy with nginx + TLS](#reverse-proxy-with-nginx--tls)
- [Cloud platforms](#cloud-platforms)
- [Configuration reference](#configuration-reference)

---

## Local development

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
git clone https://github.com/spotticusprime/scry
cd scry
dotnet run --project src/Scry.Host
```

The `ASPNETCORE_ENVIRONMENT` defaults to `Development`, which picks up `appsettings.Development.json`:
- Database path: `scry-dev.db` in the current working directory
- EF Core SQL logging enabled
- Verbose log level

The API is available at `http://localhost:5000`. A health endpoint is at `http://localhost:5000/healthz`.

To use a different port:

```bash
ASPNETCORE_URLS=http://localhost:7000 dotnet run --project src/Scry.Host
```

### Running tests

```bash
dotnet test
```

---

## Production build (bare metal / VPS)

### Self-contained single-file binary

Produces a single executable that bundles the .NET runtime — no SDK or runtime installation required on the target machine.

```bash
dotnet publish src/Scry.Host \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  --output ./publish
```

Available runtime identifiers (RIDs): `linux-x64`, `linux-arm64`, `win-x64`, `osx-x64`, `osx-arm64`.

Copy the `publish/` directory to your server:

```bash
scp -r ./publish user@server:/opt/scry
ssh user@server "chmod +x /opt/scry/Scry.Host"
```

### Framework-dependent binary

Smaller artifact; requires .NET 10 runtime on the target.

```bash
dotnet publish src/Scry.Host \
  --configuration Release \
  --output ./publish
```

Install the runtime on Ubuntu/Debian:

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-10.0
```

---

## Linux systemd service

Create a dedicated user to run the service with minimal privileges.

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin scry
sudo mkdir -p /opt/scry /var/lib/scry
sudo chown scry:scry /var/lib/scry
```

Copy the published binary:

```bash
sudo cp -r ./publish/* /opt/scry/
sudo chown -R root:root /opt/scry
sudo chmod +x /opt/scry/Scry.Host
```

Create the service unit at `/etc/systemd/system/scry.service`:

```ini
[Unit]
Description=Scry monitoring service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=scry
Group=scry
WorkingDirectory=/opt/scry

# Database stored in /var/lib/scry
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Scry__DatabasePath=/var/lib/scry/scry.db

# Listen on localhost only; nginx handles public TLS termination
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000

ExecStart=/opt/scry/Scry.Host

# Restart policy
Restart=on-failure
RestartSec=10s

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/var/lib/scry
ProtectHome=true
CapabilityBoundingSet=

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable scry
sudo systemctl start scry
sudo systemctl status scry
```

View logs:

```bash
sudo journalctl -u scry -f
```

---

## Docker

### Dockerfile

Create `Dockerfile` at the repo root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Scry.Host \
    --configuration Release \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user
RUN adduser --disabled-password --gecos "" --no-create-home scry
USER scry

COPY --from=build --chown=scry:scry /app .

VOLUME /data
ENV Scry__DatabasePath=/data/scry.db
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["./Scry.Host"]
```

Build and run:

```bash
docker build -t scry:latest .

docker run -d \
  --name scry \
  -p 8080:8080 \
  -v scry_data:/data \
  scry:latest
```

The database is persisted in a named volume `scry_data`. To use a host directory instead:

```bash
docker run -d \
  --name scry \
  -p 8080:8080 \
  -v /srv/scry:/data \
  scry:latest
```

---

## Docker Compose

`docker-compose.yml`:

```yaml
services:
  scry:
    build: .
    image: scry:latest
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    volumes:
      - scry_data:/data
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Scry__DatabasePath: /data/scry.db
      Scry__Runner__PollInterval: "00:00:05"
      Scry__Runner__LeaseDuration: "00:05:00"
    healthcheck:
      # Uses wget (available in the aspnet runtime image) rather than curl.
      test: ["CMD", "wget", "--quiet", "--spider", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s

volumes:
  scry_data:
```

```bash
docker compose up -d
docker compose logs -f
```

---

## Reverse proxy with nginx + TLS

Scry listens on a local port. nginx terminates TLS and proxies to it.

Install nginx and certbot (Ubuntu/Debian):

```bash
sudo apt-get install -y nginx certbot python3-certbot-nginx
```

Create `/etc/nginx/sites-available/scry`:

```nginx
server {
    listen 80;
    server_name scry.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name scry.example.com;

    ssl_certificate     /etc/letsencrypt/live/scry.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/scry.example.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;

    # Security headers
    add_header Strict-Transport-Security "max-age=63072000; includeSubDomains" always;
    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }
}
```

Activate and issue a certificate:

```bash
sudo ln -s /etc/nginx/sites-available/scry /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
sudo certbot --nginx -d scry.example.com
```

Certbot modifies the nginx config to manage the certificate. Auto-renewal is configured by the certbot systemd timer (`certbot.timer`).

---

## Cloud platforms

### Fly.io

Fly deploys Docker containers globally. The free tier is sufficient for personal or small-team use.

```bash
# Install flyctl: https://fly.io/docs/getting-started/installing-flyctl/
fly auth login
fly launch --name scry --no-deploy
```

This creates `fly.toml`. Edit it:

```toml
app = "scry"
primary_region = "ord"

[build]

[env]
  ASPNETCORE_ENVIRONMENT = "Production"

[mounts]
  source = "scry_data"
  destination = "/data"

[[services]]
  protocol = "tcp"
  internal_port = 8080

  [[services.ports]]
    port = 443
    handlers = ["tls", "http"]

  [[services.ports]]
    port = 80
    handlers = ["http"]

  [services.http_checks]
    interval = "30s"
    timeout = "5s"
    grace_period = "10s"
    path = "/healthz"
```

Create the volume and deploy:

```bash
fly volumes create scry_data --size 1
fly deploy
```

Set environment variables (e.g. custom database path):

```bash
fly secrets set Scry__DatabasePath=/data/scry.db
```

### Railway

Railway supports automatic deployments from GitHub. Push the Dockerfile to your repo and Railway will build and deploy it.

1. Create a new project on [railway.app](https://railway.app)
2. Connect your GitHub repo
3. Set environment variables in the Railway dashboard:
   - `Scry__DatabasePath` = `/data/scry.db`
   - `ASPNETCORE_ENVIRONMENT` = `Production`
4. Add a persistent volume mounted at `/data`

### Generic VPS (Hetzner, DigitalOcean, Linode, etc.)

The recommended approach for a VPS is:

1. Publish a self-contained binary (`dotnet publish --self-contained`)
2. Copy to `/opt/scry/`
3. Run as a systemd service (see [Linux systemd service](#linux-systemd-service))
4. Put nginx in front for TLS (see [Reverse proxy](#reverse-proxy-with-nginx--tls))

A 2 GB RAM / 1 vCPU instance ($5–6/month) is more than sufficient for hundreds of probes running at 5-minute intervals.

---

## Configuration reference

Configuration is read from (in priority order):
1. Environment variables
2. `appsettings.{Environment}.json`
3. `appsettings.json`

Environment variables use double underscore as the separator: `Scry__DatabasePath` maps to `Scry:DatabasePath`.

| Key | Default | Notes |
|-----|---------|-------|
| `Scry:DatabasePath` | `%APPDATA%/scry/scry.db` (Windows) or `~/.config/scry/scry.db` (Linux/macOS) | Path to the SQLite database file. Parent directory is created automatically. |
| `Scry:Runner:PollInterval` | `00:00:05` | How often the job runner polls for ready jobs |
| `Scry:Runner:LeaseDuration` | `00:05:00` | How long a job lease is held before another runner can claim it |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Bind address(es), e.g. `http://+:8080` or `http://127.0.0.1:5000` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` for verbose logging and dev defaults |

### Database notes

- Scry uses **SQLite** — no external database server required.
- The database file is auto-created and migrations are applied on startup.
- For backup, a simple file copy while the service is stopped is sufficient. For hot backups use SQLite's `.backup` command or `litestream`.
- SQLite is designed for single-writer workloads. Running multiple Scry instances against the same database file is not supported.

### Litestream (streaming backup)

[Litestream](https://litestream.io) replicates SQLite databases to S3-compatible storage in real time. This is the recommended approach for off-host backup without downtime.

```yaml
# /etc/litestream.yml
dbs:
  - path: /var/lib/scry/scry.db
    replicas:
      - url: s3://my-bucket/scry/scry.db
```

```bash
litestream replicate -config /etc/litestream.yml
```

# Workflow

## Branches

One topic per branch. Names follow `area/short-slug`:

- `bootstrap/` — repo setup, tooling
- `core/` — domain model, interfaces
- `data/` — EF Core, migrations
- `jobs/` — queue, scheduling
- `probes/` — probe engine, built-in probes
- `alerts/` — alert pipeline, notifiers
- `runner/` — runner interface, in-process and remote runners
- `api/` — ASP.NET Core API
- `web/` — Blazor UI
- `host/` — composition root, CLI entry point
- `build/` — build tooling, CI, release
- `docs/` — documentation
- `overlay/` — fork/overlay examples

## Commits

Imperative mood. Subject line under ~70 chars, says what changed. Body only when the *why* isn't obvious from the diff. No conventional-commit prefixes. No emoji.

Good:

    Add cert expiry probe

Not:

    feat(probes): ✨ implement SSL certificate expiration monitoring

## Pull requests

Every change lands via PR. The PR description has three sections:

1. **What** — one or two sentences on the change.
2. **Why** — reasoning and trade-offs.
3. **Consulted during development** — anyone who gave input, with a one-liner on what they contributed. Includes external reviewers (human or AI).

PRs need at least one external approval before merge. Squash-merge by default; use rebase-and-merge when the branch's individual commits tell a story worth preserving in main's history.

## Review

For AI-assisted review, pull the diff and paste it with the prompt in `docs/review-prompt.md` into whichever model you want. Treat responses as advice — incorporate what makes sense, note dismissals in the PR discussion alongside what was taken.

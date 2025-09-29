# Next Workspace

Services under `next/`:

- backend: Rust (Axum + SQLx Postgres). Exposes `/health`, `/v1/ping`, `/v1/users`.
- web: Next.js 14 app router (TypeScript). Reads `NEXT_PUBLIC_API_URL`.
- mcp: Express service for AI to query DB (`POST /sql`) and proxy VRChat API (`GET /vrchat?url=...`).
- db: Postgres 16.

## Quickstart

1. Copy envs:

```bash
cp .env.example .env
```

2. Start stack (requires Docker):

```bash
docker compose -f compose.yml up --build
```

- Web: http://localhost:3000
- Backend: http://localhost:8080/health
- MCP: http://localhost:8765/health

## Notes
- Backend runs SQLx embedded migrations on startup.
- MCP can access VRChat API when `VRCHAT_AUTH_COOKIE` is provided.
- Original project VR integrations are not used in this new stack.
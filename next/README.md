VRCX Next

A backend-focused reimplementation using Rust (Axum + SQLx) and a modern Next.js frontend, with a simple MCP service for AI access to DB and VRChat API.

Services
- Backend: Rust + Axum + SQLx (SQLite)
- Frontend: Next.js (App Router) + Tailwind CSS
- MCP: Node + Express + Axios

Quickstart (Docker)

1. Copy env

```bash
cp .env.example .env
```

2. Run

```bash
docker compose up --build
```

- Backend: http://localhost:8080/healthz
- Frontend: http://localhost:3000
- MCP: http://localhost:7070/healthz

Environment variables (.env)

```
# Backend
RUST_LOG=info
DATABASE_URL=sqlite:///app/data/vrcx.db

# Frontend
NEXT_PUBLIC_BACKEND_URL=http://localhost:8080
NEXT_TELEMETRY_DISABLED=1

# MCP
BACKEND_URL=http://backend:8080
VRCHAT_BASE_URL=https://api.vrchat.cloud/api/1
VRCHAT_AUTH_COOKIE=
```

Endpoints

- Backend
  - GET `/healthz`
  - GET `/api/notes` (optional `user_id`)
  - POST `/api/notes` { user_id, memo }
  - GET `/api/favorites/worlds`
  - GET `/api/favorites/avatars`
  - GET `/api/feed/recent?limit=50`

- MCP
  - GET `/mcp/notes` -> backend `/api/notes`
  - POST `/mcp/notes` -> backend `/api/notes`
  - GET `/mcp/favorites/worlds` -> backend `/api/favorites/worlds`
  - GET `/mcp/favorites/avatars` -> backend `/api/favorites/avatars`
  - GET `/mcp/feed/recent` -> backend `/api/feed/recent`
  - GET `/mcp/vrchat/users/:id` -> VRChat API

Development
- Hot reload for backend via `cargo watch`
- Next.js dev server on port 3000
- MCP runs with tsx watch

Notes
- Only the web stack is preserved; VR/OpenVR, Cef/Electron bindings, and Windows-specific interop are removed.
- SQLite schema is minimized and can be expanded as needed.

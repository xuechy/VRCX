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

Development
- Hot reload for backend via `cargo watch`
- Next.js dev server on port 3000
- MCP runs with tsx watch

Notes
- Only the web stack is preserved; VR/OpenVR, Cef/Electron bindings, and Windows-specific interop are removed.
- SQLite schema is minimized and can be expanded as needed.

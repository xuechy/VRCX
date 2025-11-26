import express, { Request, Response } from 'express';

const app = express();
app.use(express.json());

const PORT = process.env.PORT ? Number(process.env.PORT) : 7070;
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:8080';
const VRCHAT_BASE_URL = process.env.VRCHAT_BASE_URL || 'https://api.vrchat.cloud/api/1';
const VRCHAT_AUTH_COOKIE = process.env.VRCHAT_AUTH_COOKIE || '';

app.get('/healthz', (_req: Request, res: Response) => {
  res.json({ ok: true, service: 'mcp', backend: BACKEND_URL });
});

// Proxy to backend for DB-driven endpoints
app.get('/mcp/notes', async (req: Request, res: Response) => {
  try {
    const url = new URL(`${BACKEND_URL}/api/notes`);
    for (const [k, v] of Object.entries(req.query)) {
      if (Array.isArray(v)) {
        v.forEach((vv) => url.searchParams.append(k, String(vv)));
      } else if (v !== undefined) {
        url.searchParams.set(k, String(v));
      }
    }
    const r = await fetch(url, { method: 'GET' });
    const data = await r.json();
    res.status(r.status).json(data);
  } catch (e: any) {
    res.status(500).json({ error: e?.message || 'backend error' });
  }
});

app.post('/mcp/notes', async (req: Request, res: Response) => {
  try {
    const r = await fetch(`${BACKEND_URL}/api/notes`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(req.body),
    });
    const text = await r.text();
    res.status(r.status).send(text || {});
  } catch (e: any) {
    res.status(e?.response?.status || 500).json({ error: e?.message || 'backend error' });
  }
});

app.get('/mcp/favorites/worlds', async (_req: Request, res: Response) => {
  try {
    const r = await fetch(`${BACKEND_URL}/api/favorites/worlds`);
    const data = await r.json();
    res.status(r.status).json(data);
  } catch (e: any) {
    res.status(500).json({ error: e?.message || 'backend error' });
  }
});

app.get('/mcp/favorites/avatars', async (_req: Request, res: Response) => {
  try {
    const r = await fetch(`${BACKEND_URL}/api/favorites/avatars`);
    const data = await r.json();
    res.status(r.status).json(data);
  } catch (e: any) {
    res.status(500).json({ error: e?.message || 'backend error' });
  }
});

app.get('/mcp/feed/recent', async (req: Request, res: Response) => {
  try {
    const url = new URL(`${BACKEND_URL}/api/feed/recent`);
    for (const [k, v] of Object.entries(req.query)) {
      if (Array.isArray(v)) {
        v.forEach((vv) => url.searchParams.append(k, String(vv)));
      } else if (v !== undefined) {
        url.searchParams.set(k, String(v));
      }
    }
    const r = await fetch(url, { method: 'GET' });
    const data = await r.json();
    res.status(r.status).json(data);
  } catch (e: any) {
    res.status(500).json({ error: e?.message || 'backend error' });
  }
});

// Minimal VRChat API fetch through MCP with cookie-based auth if provided
app.get('/mcp/vrchat/users/:id', async (req: Request, res: Response) => {
  try {
    const headers: Record<string, string> = {};
    if (VRCHAT_AUTH_COOKIE) headers['Cookie'] = VRCHAT_AUTH_COOKIE;
    const r = await fetch(`${VRCHAT_BASE_URL}/users/${req.params.id}`, { headers });
    const data = await r.json();
    res.status((r as any).status || 200).json(data);
  } catch (e: any) {
    res.status(e?.response?.status || 500).json({ error: e?.message || 'vrchat api error' });
  }
});

app.listen(PORT, () => {
  console.log(`[mcp] listening on :${PORT}`);
});


import express from 'express';
import axios from 'axios';

const app = express();
app.use(express.json());

const PORT = process.env.PORT ? Number(process.env.PORT) : 7070;
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:8080';
const VRCHAT_BASE_URL = process.env.VRCHAT_BASE_URL || 'https://api.vrchat.cloud/api/1';
const VRCHAT_AUTH_COOKIE = process.env.VRCHAT_AUTH_COOKIE || '';

app.get('/healthz', (_req, res) => {
  res.json({ ok: true, service: 'mcp', backend: BACKEND_URL });
});

// Proxy to backend for DB-driven endpoints
app.get('/mcp/notes', async (req, res) => {
  try {
    const r = await axios.get(`${BACKEND_URL}/api/notes`, { params: req.query });
    res.status(r.status).json(r.data);
  } catch (e: any) {
    res.status(500).json({ error: e?.message || 'backend error' });
  }
});

// Minimal VRChat API fetch through MCP with cookie-based auth if provided
app.get('/mcp/vrchat/users/:id', async (req, res) => {
  try {
    const headers: Record<string, string> = {};
    if (VRCHAT_AUTH_COOKIE) headers['Cookie'] = VRCHAT_AUTH_COOKIE;
    const r = await axios.get(`${VRCHAT_BASE_URL}/users/${req.params.id}`, { headers });
    res.status(r.status).json(r.data);
  } catch (e: any) {
    res.status(e?.response?.status || 500).json({ error: e?.message || 'vrchat api error' });
  }
});

app.listen(PORT, () => {
  console.log(`[mcp] listening on :${PORT}`);
});


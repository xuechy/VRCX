import express from 'express';
import fetch from 'node-fetch';
import pkg from 'pg';
const { Pool } = pkg;

const app = express();
app.use(express.json());

const pool = new Pool({ connectionString: process.env.DATABASE_URL });

app.get('/health', (req, res) => res.json({ ok: true }));

app.post('/sql', async (req, res) => {
  const { text, params } = req.body || {};
  if (!text) return res.status(400).json({ error: 'text required' });
  try {
    const result = await pool.query(text, params || []);
    res.json({ rows: result.rows });
  } catch (e) {
    res.status(400).json({ error: String(e) });
  }
});

app.get('/vrchat', async (req, res) => {
  const url = req.query.url;
  if (!url || typeof url !== 'string') return res.status(400).json({ error: 'url required' });
  try {
    const headers = {};
    if (process.env.VRCHAT_AUTH_COOKIE) headers['Cookie'] = process.env.VRCHAT_AUTH_COOKIE;
    const r = await fetch(url, { headers });
    const data = await r.json();
    res.json(data);
  } catch (e) {
    res.status(400).json({ error: String(e) });
  }
});

const port = process.env.PORT || 8765;
app.listen(port, () => console.log(`MCP listening on ${port}`));
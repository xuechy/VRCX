/// <reference types="node" />
import React from 'react';

type FeedItem = [number, string, string];

async function getFeed(limit = 50): Promise<FeedItem[] | null> {
  try {
    const base = process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:8080';
    const res = await fetch(`${base}/api/feed/recent?limit=${limit}`, { cache: 'no-store' });
    if (!res.ok) return null;
    return (await res.json()) as FeedItem[];
  } catch {
    return null;
  }
}

export default async function FeedPage() {
  const items = await getFeed(50);
  return (
    <main className="container mx-auto p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Recent Feed</h1>
      {!items || items.length === 0 ? (
        <div className="text-slate-400">No feed items.</div>
      ) : (
        <ul className="space-y-3">
          {items.map(([id, created_at, data]) => (
            <li key={id} className="rounded border border-slate-800 bg-slate-900/50 p-3">
              <div className="text-xs text-slate-500">{created_at}</div>
              <pre className="mt-2 whitespace-pre-wrap text-xs">{data}</pre>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}


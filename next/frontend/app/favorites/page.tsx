/// <reference types="node" />
import React from 'react';

type FavoriteItem = {
  id: number;
  created_at: string;
  item_id: string;
  group_name?: string | null;
};

async function fetchJson<T>(path: string): Promise<T | null> {
  try {
    const base = process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:8080';
    const res = await fetch(`${base}${path}`, { cache: 'no-store' });
    if (!res.ok) return null;
    return (await res.json()) as T;
  } catch {
    return null;
  }
}

export default async function FavoritesPage() {
  const [worlds, avatars] = await Promise.all([
    fetchJson<FavoriteItem[]>(`/api/favorites/worlds`),
    fetchJson<FavoriteItem[]>(`/api/favorites/avatars`),
  ]);

  return (
    <main className="container mx-auto p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Favorites</h1>
      <section className="space-y-3">
        <h2 className="text-xl font-medium">Worlds</h2>
        {!worlds || worlds.length === 0 ? (
          <div className="text-slate-400">No favorite worlds.</div>
        ) : (
          <ul className="space-y-2">
            {worlds.map((w) => (
              <li key={w.id} className="rounded border border-slate-800 bg-slate-900/50 p-3">
                <div className="text-sm">World ID: {w.item_id}</div>
                <div className="text-xs text-slate-500">Added: {w.created_at}</div>
                {w.group_name ? <div className="text-xs text-slate-400">Group: {w.group_name}</div> : null}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="text-xl font-medium">Avatars</h2>
        {!avatars || avatars.length === 0 ? (
          <div className="text-slate-400">No favorite avatars.</div>
        ) : (
          <ul className="space-y-2">
            {avatars.map((a) => (
              <li key={a.id} className="rounded border border-slate-800 bg-slate-900/50 p-3">
                <div className="text-sm">Avatar ID: {a.item_id}</div>
                <div className="text-xs text-slate-500">Added: {a.created_at}</div>
                {a.group_name ? <div className="text-xs text-slate-400">Group: {a.group_name}</div> : null}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}


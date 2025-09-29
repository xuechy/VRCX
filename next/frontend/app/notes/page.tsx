/// <reference types="node" />
import React from 'react';

async function getNotes() {
  try {
    const base = process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:8080';
    const res = await fetch(`${base}/api/notes`, { cache: 'no-store' });
    if (!res.ok) return [] as Array<any>;
    return (await res.json()) as Array<{ user_id: string; edited_at: string; memo: string }>;
  } catch {
    return [] as Array<any>;
  }
}

export default async function NotesPage() {
  const notes = await getNotes();
  return (
    <main className="container mx-auto p-6 space-y-6">
      <h1 className="text-2xl font-semibold">Notes</h1>
      {notes.length === 0 ? (
        <div className="text-slate-400">No notes found.</div>
      ) : (
        <ul className="space-y-3">
          {notes.map((n, idx) => (
            <li key={`${n.user_id}-${idx}`} className="rounded border border-slate-800 bg-slate-900/50 p-3">
              <div className="text-sm text-slate-400">User: {n.user_id}</div>
              <div className="text-xs text-slate-500">Edited: {n.edited_at}</div>
              <div className="mt-2 whitespace-pre-wrap text-sm">{n.memo}</div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}


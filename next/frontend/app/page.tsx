/// <reference types="node" />
import React from 'react';

async function getHealth() {
  try {
    const base = process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:8080';
    const res = await fetch(`${base}/healthz`, { cache: 'no-store' });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export default async function Home() {
  const health = await getHealth();
  return (
    <main className="container mx-auto p-6 space-y-4">
      <h1 className="text-3xl font-semibold">VRCX Next</h1>
      <p className="text-slate-300">A modern, web-first reimplementation.</p>
      <div className="rounded-lg border border-slate-800 p-4 bg-slate-900/50">
        <div className="text-slate-400 text-sm">Backend Health</div>
        {health ? (
          <pre className="mt-2 text-xs whitespace-pre-wrap">{JSON.stringify(health, null, 2)}</pre>
        ) : (
          <div className="mt-2 text-red-400">Cannot reach backend at /healthz</div>
        )}
      </div>
    </main>
  );
}


export default async function Page() {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  let status = 'unknown';
  try {
    const res = await fetch(`${apiUrl}/health`, { cache: 'no-store' });
    const json = await res.json();
    status = json?.ok ? 'ok' : 'down';
  } catch (e) {
    status = 'down';
  }
  return (
    <main style={{ padding: 24, fontFamily: 'sans-serif' }}>
      <h1>New Frontend</h1>
      <p>Backend health: {status}</p>
    </main>
  );
}
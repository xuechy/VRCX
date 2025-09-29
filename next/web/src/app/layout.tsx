export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body style={{ margin: 0, fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif' }}>
        {children}
      </body>
    </html>
  );
}
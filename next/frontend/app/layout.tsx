import './globals.css'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'VRCX Next',
  description: 'Modern frontend for VRCX Next',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-slate-950 text-slate-100">{children}</body>
    </html>
  )
}


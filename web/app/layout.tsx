import type { Metadata } from 'next'
import { cinzel, inter } from '../lib/fonts'
import '../styles/globals.css'

export const metadata: Metadata = {
  title: 'AlphaScope',
  description: 'FFXIV player lookup — scanned, scrolled, remembered.',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${cinzel.variable} ${inter.variable}`}>
      <body>
        <div className="min-h-screen flex flex-col">
          {children}
        </div>
      </body>
    </html>
  )
}

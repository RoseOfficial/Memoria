import type { Metadata } from 'next'
import { cinzel, inter } from '../lib/fonts'
import '../styles/globals.css'
import { Nav } from '../components/nav/Nav'
import { Footer } from '../components/nav/Footer'

export const metadata: Metadata = {
  title: 'AlphaScope',
  description: 'FFXIV player lookup — scanned, scrolled, remembered.',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${cinzel.variable} ${inter.variable}`}>
      <body>
        <div className="min-h-screen flex flex-col">
          <Nav />
          <div className="flex-1">{children}</div>
          <Footer />
        </div>
      </body>
    </html>
  )
}

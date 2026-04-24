import { Cinzel, Inter } from 'next/font/google'

export const cinzel = Cinzel({
  subsets: ['latin'],
  weight: ['400', '600', '700'],
  variable: '--font-display',
  display: 'swap',
})

export const inter = Inter({
  subsets: ['latin'],
  variable: '--font-body',
  display: 'swap',
})

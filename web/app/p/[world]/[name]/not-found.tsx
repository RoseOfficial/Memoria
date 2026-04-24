import Link from 'next/link'
import { SearchBox } from '../../../../components/forms/SearchBox'

export default function PlayerNotFound() {
  return (
    <main className="max-w-xl mx-auto px-8 py-16 text-center space-y-6">
      <h1 className="text-3xl">Player not found</h1>
      <p className="text-[var(--color-text-muted)]">
        We can't find that player on AlphaScope. They may not have been scanned yet, or the name may be misspelled.
      </p>
      <SearchBox />
      <p><Link href="/" className="text-[var(--color-gold)]">← Back home</Link></p>
    </main>
  )
}

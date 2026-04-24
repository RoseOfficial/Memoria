import { apiFetchJson } from '../lib/api'
import type { RecentPlayerResponse } from '../lib/types'
import { SearchBox } from '../components/forms/SearchBox'
import { SignatureFrame } from '../components/ornaments/SignatureFrame'
import { toSlug } from '../lib/slug'
import Link from 'next/link'

export default async function Home() {
  const recent = await apiFetchJson<RecentPlayerResponse>('/v1/players/recent').catch(() => null)

  return (
    <main>
      <SignatureFrame className="max-w-3xl mx-auto mt-16 py-16 px-8">
        <div className="text-center space-y-6">
          <h1 className="text-5xl tracking-widest">Memoria</h1>
          <p className="text-lg text-[var(--color-text-muted)] max-w-xl mx-auto">
            FFXIV player lookup — scanned, scrolled, remembered.
          </p>
          <div className="max-w-lg mx-auto pt-4">
            <SearchBox autoFocus />
          </div>
        </div>
      </SignatureFrame>

      {recent && recent.items.length > 0 && (
        <section className="max-w-3xl mx-auto px-8 mt-16">
          <h2 className="text-xs tracking-widest text-[var(--color-gold)] uppercase mb-4">Recent scans</h2>
          <ul className="divide-y divide-[var(--color-bg-elevated)]">
            {recent.items.slice(0, 8).map((p) => (
              <li key={p.name + p.worldSlug} className="py-3 flex justify-between text-sm">
                <Link href={`/p/${p.worldSlug}/${toSlug(p.name)}`} className="text-[var(--color-cream)] hover:text-[var(--color-gold)]">
                  {p.name}
                </Link>
                <span className="text-[var(--color-text-muted)]">{p.worldName}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </main>
  )
}

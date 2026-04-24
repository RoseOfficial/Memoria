import { apiFetchJson } from '../../lib/api'
import { SearchBox } from '../../components/forms/SearchBox'
import { toSlug } from '../../lib/slug'
import Link from 'next/link'

type SearchResponse = {
  items: {
    localContentId: number
    name: string
    worldSlug: string
    worldName: string
    avatarUrl: string | null
  }[]
}

export default async function SearchPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams
  const q = (params.q ?? '').trim()
  const results = q
    ? await apiFetchJson<SearchResponse>(`/v1/players/search?q=${encodeURIComponent(q)}`).catch(() => ({ items: [] }))
    : null

  const items = results?.items ?? []

  return (
    <main className="max-w-3xl mx-auto px-8 py-12 space-y-8">
      <h1 className="text-3xl">Search</h1>
      <SearchBox />
      {q && (
        <div>
          <p className="text-[var(--color-text-muted)] text-sm mb-4">
            {items.length} result{items.length === 1 ? '' : 's'} for &ldquo;{q}&rdquo;
          </p>
          <ul className="divide-y divide-[var(--color-bg-elevated)]">
            {items.map((r) => (
              <li key={r.localContentId} className="py-3 flex justify-between text-sm">
                <Link
                  href={`/p/${r.worldSlug}/${toSlug(r.name)}`}
                  className="text-[var(--color-cream)] hover:text-[var(--color-gold)]"
                >
                  {r.name}
                </Link>
                <span className="text-[var(--color-text-muted)]">{r.worldName}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </main>
  )
}

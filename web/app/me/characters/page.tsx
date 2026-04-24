import { apiFetchJson } from '../../../lib/api'
import { toSlug } from '../../../lib/slug'
import Link from 'next/link'

type CharacterRow = {
  localContentId: number
  name: string
  worldSlug: string
  worldName: string
  avatarUrl: string | null
  hideAlts: boolean
  hideEncounters: boolean
  hideEntirely: boolean
  claimedAt: string | null
}

export default async function CharactersPage() {
  const characters = await apiFetchJson<CharacterRow[]>('/v1/users/me/characters') ?? []

  return (
    <main className="max-w-4xl mx-auto px-8 py-12 space-y-8">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl">Your characters</h1>
        {/* Add Character button wired in Task 41 */}
      </div>

      {characters.length === 0 ? (
        <p className="text-[var(--color-text-muted)]">You haven&apos;t claimed any characters yet.</p>
      ) : (
        <div className="space-y-4">
          {characters.map((c) => (
            <div key={c.localContentId} className="flex items-center gap-4 p-4 bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)]">
              <div className="w-16 h-16 bg-[var(--color-bg-elevated)] border border-[var(--color-gold)] rounded flex-shrink-0" />
              <div className="flex-1">
                <Link
                  href={`/p/${c.worldSlug}/${toSlug(c.name)}`}
                  className="text-lg text-[var(--color-cream)] hover:text-[var(--color-gold)]"
                >
                  {c.name}
                </Link>
                <p className="text-sm text-[var(--color-text-muted)]">{c.worldName}</p>
              </div>
              <div className="text-sm text-[var(--color-text-dim)]">
                {c.hideAlts && <span className="mr-2">hide alts</span>}
                {c.hideEncounters && <span className="mr-2">hide encounters</span>}
                {c.hideEntirely && <span className="mr-2">HIDDEN</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </main>
  )
}

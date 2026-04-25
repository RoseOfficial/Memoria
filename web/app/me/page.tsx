import { getMe } from '../../lib/auth'
import { apiFetchJson } from '../../lib/api'
import { toSlug } from '../../lib/slug'
import type { ContributionsResponse } from '../../lib/types'
import { SignatureFrame } from '../../components/ornaments/SignatureFrame'
import Link from 'next/link'

// Server-rendered relative time. Stale once the page sits open for a while, but
// the recent-contributions list isn't real-time critical and the user reloads
// the dashboard often enough that drift doesn't matter.
function formatScannedAt(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime()
  const seconds = Math.floor(diffMs / 1000)
  if (seconds < 60) return 'just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  return new Date(iso).toLocaleDateString()
}

export default async function MePage() {
  const me = (await getMe())!  // layout redirects if null
  // gameAccountId: 0 means unlinked (plugin default), positive means linked to an in-game account
  const isLinked = me.gameAccountId != null && me.gameAccountId !== 0
  const contributions = isLinked
    ? await apiFetchJson<ContributionsResponse>('/v1/users/me/contributions').catch(() => null)
    : null

  return (
    <main className="max-w-4xl mx-auto px-8 py-12 space-y-8">
      <h1 className="text-3xl">Dashboard</h1>

      {!isLinked ? (
        <SignatureFrame className="p-8 space-y-4">
          <h2 className="text-xl">Link your in-game character</h2>
          <p className="text-[var(--color-text-muted)]">
            Open the Memoria plugin in-game, go to <em>Settings</em>, click <em>Generate web link code</em>.
            Paste the code (starts with <code className="text-[var(--color-gold)]">AL-</code>) on the link page below.
          </p>
          <Link href="/me/link" className="inline-block text-[var(--color-gold)] hover:text-[var(--color-gold-bright)]">
            Go to link redeem page →
          </Link>
        </SignatureFrame>
      ) : (
        <>
          <div className="grid md:grid-cols-3 gap-4">
            <div className="bg-[var(--color-bg-raised)] p-6 border border-[var(--color-bg-elevated)]">
              <p className="text-xs uppercase tracking-widest text-[var(--color-gold)] mb-2">Lifetime contributions</p>
              <p className="text-3xl">{contributions?.lifetime.toLocaleString() ?? '—'}</p>
            </div>
            <Link href="/me/characters" className="bg-[var(--color-bg-raised)] p-6 border border-[var(--color-bg-elevated)] hover:border-[var(--color-gold)] transition-colors">
              <p className="text-xs uppercase tracking-widest text-[var(--color-gold)] mb-2">Your characters</p>
              <p className="text-[var(--color-text-muted)]">Manage claims & privacy →</p>
            </Link>
            <Link href="/me/link" className="bg-[var(--color-bg-raised)] p-6 border border-[var(--color-bg-elevated)] hover:border-[var(--color-gold)] transition-colors">
              <p className="text-xs uppercase tracking-widest text-[var(--color-gold)] mb-2">Link another plugin</p>
              <p className="text-[var(--color-text-muted)]">Redeem AL- code →</p>
            </Link>
          </div>

          {contributions?.recent && contributions.recent.length > 0 && (
            <section className="bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] p-6">
              <p className="text-xs uppercase tracking-widest text-[var(--color-gold)] mb-4">Recent contributions</p>
              <ul className="divide-y divide-[var(--color-bg-elevated)]">
                {contributions.recent.map((r) => (
                  <li key={`${r.worldSlug}/${r.playerName}`} className="py-3 flex justify-between items-center gap-4">
                    <Link
                      href={`/p/${r.worldSlug}/${toSlug(r.playerName)}`}
                      className="hover:text-[var(--color-gold)] truncate"
                    >
                      {r.playerName}
                      <span className="text-[var(--color-text-muted)] ml-2">{r.worldName}</span>
                    </Link>
                    <time
                      dateTime={r.scannedAt}
                      title={new Date(r.scannedAt).toLocaleString()}
                      className="text-sm text-[var(--color-text-muted)] shrink-0"
                    >
                      {formatScannedAt(r.scannedAt)}
                    </time>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </>
      )}
    </main>
  )
}

import { apiFetchJson } from '../../../lib/api'
import type { TakedownListItem } from '../../../lib/types'
import { TakedownRow } from './TakedownRow'

export default async function AdminTakedownsPage() {
  const pending = await apiFetchJson<TakedownListItem[]>('/v1/takedowns?status=pending') ?? []

  return (
    <main className="max-w-4xl mx-auto px-8 py-12 space-y-6">
      <h1 className="text-3xl">Takedown triage</h1>
      <p className="text-[var(--color-text-muted)]">
        {pending.length} pending request{pending.length === 1 ? '' : 's'}
      </p>
      {pending.length === 0 ? (
        <p className="text-[var(--color-text-dim)] italic">No pending requests.</p>
      ) : (
        <div className="space-y-4">
          {pending.map((row) => <TakedownRow key={row.id} row={row} />)}
        </div>
      )}
    </main>
  )
}

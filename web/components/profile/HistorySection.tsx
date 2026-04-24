import type { NameHistoryEntry, WorldHistoryEntry } from '../../lib/types'
import { SectionCard } from './JobsSection'

export function HistorySection({ names, worlds }: { names: NameHistoryEntry[] | null; worlds: WorldHistoryEntry[] | null }) {
  const hasAny = (names?.length ?? 0) + (worlds?.length ?? 0) > 0
  return (
    <SectionCard title="Name / World History">
      {!hasAny ? (
        <p className="text-sm text-[var(--color-text-dim)]">No renames or transfers recorded.</p>
      ) : (
        <div className="text-sm space-y-2">
          {names && names.length > 0 && (
            <div>
              <p className="text-xs uppercase tracking-wider text-[var(--color-text-dim)] mb-1">Previous names</p>
              {names.map((n, i) => (
                <p key={i} className="text-[var(--color-text-muted)]">&quot;{n.name}&quot; · {new Date(n.changedAt).toLocaleDateString()}</p>
              ))}
            </div>
          )}
          {worlds && worlds.length > 0 && (
            <div>
              <p className="text-xs uppercase tracking-wider text-[var(--color-text-dim)] mb-1">Previous worlds</p>
              {worlds.map((w, i) => (
                <p key={i} className="text-[var(--color-text-muted)]">{w.worldName} · {new Date(w.changedAt).toLocaleDateString()}</p>
              ))}
            </div>
          )}
        </div>
      )}
    </SectionCard>
  )
}

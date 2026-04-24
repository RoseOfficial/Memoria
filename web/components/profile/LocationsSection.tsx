import type { LocationsData } from '../../lib/types'
import { SectionCard } from './JobsSection'

export function LocationsSection({ data }: { data: LocationsData }) {
  return (
    <SectionCard title="Recent Locations">
      {data.top.length === 0 ? (
        <p className="text-sm text-[var(--color-text-dim)]">No territory data yet.</p>
      ) : (
        <ul className="text-sm space-y-1">
          {data.top.map((t) => (
            <li key={t.territoryId} className="flex justify-between text-[var(--color-text-muted)]">
              <span>{t.territoryName}</span>
              <span className="text-[var(--color-text-dim)]">{t.visitCount}×</span>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  )
}

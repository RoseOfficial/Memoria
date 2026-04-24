import type { MinionsData } from '../../lib/types'
import { SectionCard } from './JobsSection'

export function MinionsSection({ data }: { data: MinionsData }) {
  return (
    <SectionCard title="Minions">
      <p className="text-sm text-[var(--color-text-muted)] mb-3">
        {data.collected} collected of ~{data.knownTotal} known
      </p>
      <div className="grid grid-cols-8 gap-1.5">
        {data.preview.slice(0, 16).map((m) => (
          <div
            key={m.id}
            className="aspect-square bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] rounded-sm"
            title={m.name}
          />
        ))}
      </div>
    </SectionCard>
  )
}

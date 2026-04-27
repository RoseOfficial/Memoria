import type { MountsData } from '../../lib/types'
import { SectionCard } from './JobsSection'

export function MountsSection({ data }: { data: MountsData }) {
  return (
    <SectionCard title="Mounts">
      <p className="text-sm text-[var(--color-text-muted)] mb-3">
        {data.collected} collected of ~{data.knownTotal} known
      </p>
      <div className="grid grid-cols-8 gap-1.5">
        {data.preview.slice(0, 16).map((m) => (
          <div
            key={m.id}
            className="aspect-square bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] rounded-sm overflow-hidden"
            title={m.name}
          >
            {m.iconUrl ? (
              <img src={m.iconUrl} alt={m.name} className="w-full h-full object-cover" loading="lazy" />
            ) : null}
          </div>
        ))}
      </div>
    </SectionCard>
  )
}

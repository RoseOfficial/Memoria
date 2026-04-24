import type { ReactNode } from 'react'
import type { JobsData } from '../../lib/types'

export function JobsSection({ data }: { data: JobsData }) {
  return (
    <SectionCard title="Jobs">
      {data.jobs.length === 0 ? (
        <p className="text-[var(--color-text-dim)] text-sm">No Lodestone job data captured yet.</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {data.jobs.slice(0, 8).map((j) => (
            <span key={j.name} className="px-3 py-1 text-xs border border-[var(--color-bg-elevated)] rounded">
              {j.name} {j.level}
            </span>
          ))}
          {data.jobs.length > 8 && (
            <span className="px-3 py-1 text-xs text-[var(--color-text-dim)]">+{data.jobs.length - 8}</span>
          )}
        </div>
      )}
    </SectionCard>
  )
}

export function SectionCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="bg-[var(--color-bg)] border border-[var(--color-bg-elevated)] p-6">
      <h3 className="text-xs tracking-widest text-[var(--color-gold)] uppercase mb-4 pb-2 border-b border-[var(--color-bg-elevated)]">
        {title}
      </h3>
      {children}
    </div>
  )
}

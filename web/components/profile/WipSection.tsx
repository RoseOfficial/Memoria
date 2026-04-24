import { SectionCard } from './JobsSection'

export function WipSection({ title, phase, description }: { title: string; phase: string; description: string }) {
  return (
    <SectionCard title={title}>
      <p className="text-sm text-[var(--color-text-dim)] italic">
        Capture work in progress — {phase} will {description}.
      </p>
    </SectionCard>
  )
}

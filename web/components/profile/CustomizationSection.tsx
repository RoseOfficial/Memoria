import type { CustomizationData } from '../../lib/types'
import { SectionCard } from './JobsSection'

export function CustomizationSection({ data }: { data: CustomizationData }) {
  return (
    <SectionCard title="Customization">
      <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
        <Row label="Race / Body" value={describeRace(data.genderRace)} />
        <Row label="Face" value={data.face} />
        <Row label="Skin" value={data.skinColor} />
        <Row label="Eyes" value={data.eyeShape} />
        <Row label="Height" value={data.height} />
        <Row label="Nose" value={data.nose} />
      </dl>
    </SectionCard>
  )
}

function Row({ label, value }: { label: string; value: number | string | null | undefined }) {
  return (
    <>
      <dt className="text-[var(--color-text-dim)] uppercase text-xs tracking-wider">{label}</dt>
      <dd className="text-[var(--color-text-muted)]">{value ?? '—'}</dd>
    </>
  )
}

function describeRace(genderRace: number | null | undefined): string {
  // Plan 0c: raw numeric values. Phase 1 will add a lookup when the plugin captures the richer customization set.
  return genderRace != null ? `ID ${genderRace}` : '—'
}

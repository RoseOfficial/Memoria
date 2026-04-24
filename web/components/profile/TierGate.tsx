import { SectionCard } from './JobsSection'
import Link from 'next/link'

export function TierGate({ title, tier, sectionName }: { title: string; tier: 2 | 3; sectionName: string }) {
  const copy = tier === 2
    ? `Sign in with our Discord (guild member) to see ${sectionName}.`
    : `You can only manage characters you've claimed.`
  return (
    <SectionCard title={title}>
      <p className="text-sm text-[var(--color-text-muted)] mb-2">{copy}</p>
      {tier === 2 && <Link href="/auth/signin" className="text-[var(--color-gold)] text-sm">Sign in →</Link>}
    </SectionCard>
  )
}

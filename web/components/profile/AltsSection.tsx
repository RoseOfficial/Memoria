import type { AltCharacter } from '../../lib/types'
import { SectionCard } from './JobsSection'
import Link from 'next/link'
import { toSlug } from '../../lib/slug'

export function AltsSection({ alts }: { alts: AltCharacter[] }) {
  return (
    <SectionCard title="Alt Characters">
      {alts.length === 0 ? (
        <p className="text-sm text-[var(--color-text-dim)]">No alt characters found on this account.</p>
      ) : (
        <ul className="text-sm space-y-1">
          {alts.map((a) => (
            <li key={a.localContentId}>
              <Link href={`/p/${a.worldSlug}/${toSlug(a.name)}`}
                    className="text-[var(--color-cream)] hover:text-[var(--color-gold)]">
                {a.name}
              </Link>
              <span className="text-[var(--color-text-dim)] ml-2">({a.worldName})</span>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  )
}

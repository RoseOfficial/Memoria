import Image from 'next/image'
import type { ProfileHeader } from '../../lib/types'
import { SignatureFrame } from '../ornaments/SignatureFrame'

export function ProfileHeaderCard({ header }: { header: ProfileHeader }) {
  return (
    <SignatureFrame className="p-8 bg-gradient-to-b from-[var(--color-bg-raised)] to-[var(--color-bg)] border border-[var(--color-bg-elevated)]">
      <div className="flex gap-6">
        <div className="w-24 h-24 border-2 border-[var(--color-gold)] rounded bg-[var(--color-bg-elevated)] overflow-hidden flex-shrink-0">
          {header.avatarUrl && (
            <Image src={header.avatarUrl} alt={header.name} width={96} height={96} className="w-full h-full object-cover" />
          )}
        </div>
        <div className="flex-1 space-y-1">
          <h1 className="text-3xl tracking-wider">{header.name}</h1>
          <div className="text-sm text-[var(--color-text-muted)]">
            <span className="text-[var(--color-gold)] uppercase text-xs tracking-widest mr-2">World</span>
            {header.worldName}
            {header.currentJobId != null && (
              <>
                <span className="mx-3 text-[var(--color-text-dim)]">·</span>
                <span className="text-[var(--color-gold)] uppercase text-xs tracking-widest mr-2">Job</span>
                {header.currentJobName ?? `Job ${header.currentJobId}`} Lv {header.currentJobLevel ?? '?'}
              </>
            )}
          </div>
          {header.lastSeenAt && (
            <div className="text-sm text-[var(--color-text-muted)]">
              <span className="text-[var(--color-gold)] uppercase text-xs tracking-widest mr-2">Last seen</span>
              {new Date(header.lastSeenAt).toLocaleString()}
            </div>
          )}
          {header.firstScannedAt && (
            <div className="text-sm text-[var(--color-text-muted)]">
              <span className="text-[var(--color-gold)] uppercase text-xs tracking-widest mr-2">First scanned</span>
              {new Date(header.firstScannedAt).toLocaleDateString()}
            </div>
          )}
        </div>
      </div>
    </SignatureFrame>
  )
}

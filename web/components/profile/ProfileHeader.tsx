import Image from 'next/image'
import type { ProfileHeader } from '../../lib/types'
import { SignatureFrame } from '../ornaments/SignatureFrame'
import { resolveOnlineStatus, type OnlineStatusKey } from '../../lib/data/online-status'

const STATUS_DOT_COLOR: Record<OnlineStatusKey, string> = {
  online: "bg-emerald-500",
  busy: "bg-rose-500",
  afk: "bg-amber-500",
  rp: "bg-violet-500",
  lfp: "bg-sky-500",
  sprout: "bg-lime-400",
  returner: "bg-cyan-400",
  mentor: "bg-yellow-400",
}

export function ProfileHeaderCard({ header }: { header: ProfileHeader }) {
  const status = resolveOnlineStatus(header.onlineStatusId)
  const portraitSrc = header.portraitUrl ?? header.avatarUrl
  const isFullPortrait = !!header.portraitUrl

  return (
    <SignatureFrame className="p-8 bg-gradient-to-b from-[var(--color-bg-raised)] to-[var(--color-bg)] border border-[var(--color-bg-elevated)]">
      <div className="flex gap-6 items-start">
        <div className="flex-shrink-0">
          {portraitSrc ? (
            <Image
              src={portraitSrc}
              alt={`Portrait of ${header.name}`}
              className={isFullPortrait
                ? "rounded object-cover border-2 border-[var(--color-gold)]"
                : "rounded-full object-cover border-2 border-[var(--color-gold)]"}
              width={isFullPortrait ? 280 : 96}
              height={isFullPortrait ? 383 : 96}
            />
          ) : (
            <div className="rounded w-[280px] h-[383px] bg-[var(--color-bg-elevated)] border-2 border-[var(--color-gold)] flex items-center justify-center text-[var(--color-text-dim)] text-xs">
              No portrait available
            </div>
          )}
        </div>
        <div className="flex-1 space-y-1">
          <h1 className="text-3xl tracking-wider">{header.name}</h1>
          {status && (
            <span className="inline-flex items-center gap-1.5 text-xs text-[var(--color-text-muted)]">
              <span className={`inline-block w-2 h-2 rounded-full ${STATUS_DOT_COLOR[status.key]}`} />
              {status.label}
            </span>
          )}
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

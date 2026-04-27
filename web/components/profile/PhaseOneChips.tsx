import type { ProfileHeader } from "../../lib/types"
import { resolveGrandCompany } from "../../lib/data/grand-company"

export function PhaseOneChips({ header }: { header: ProfileHeader }) {
  const gc = resolveGrandCompany(header.grandCompanyId)
  const fc = header.freeCompanyTag
  const mountName = header.currentMountName
  const mountIcon = header.currentMountIconUrl
  const minionName = header.currentMinionName
  const minionIcon = header.currentMinionIconUrl

  // Don't render the strip at all if nothing's there
  if (!gc && !fc && !mountName && !minionName) return null

  return (
    <div className="flex flex-wrap gap-2 mt-3">
      {gc && (
        <span
          className="inline-flex items-center px-2.5 py-1 rounded-md border text-xs"
          style={{ borderColor: gc.color, color: gc.color }}
        >
          {gc.label}
        </span>
      )}
      {fc && (
        <span className="inline-flex items-center px-2.5 py-1 rounded-md border border-[var(--color-border)] text-xs text-[var(--color-text-muted)] font-mono">
          «{fc}»
        </span>
      )}
      {mountName && (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md border border-[var(--color-border)] text-xs text-[var(--color-text-muted)]">
          {mountIcon && (
            <img src={mountIcon} alt="" className="w-4 h-4 inline-block" />
          )}
          Riding {mountName}
        </span>
      )}
      {minionName && (
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md border border-[var(--color-border)] text-xs text-[var(--color-text-muted)]">
          {minionIcon && (
            <img src={minionIcon} alt="" className="w-4 h-4 inline-block" />
          )}
          With {minionName}
        </span>
      )}
    </div>
  )
}

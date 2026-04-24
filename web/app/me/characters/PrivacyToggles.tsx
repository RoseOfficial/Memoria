'use client'

import { useTransition } from 'react'
import { setPrivacy, unclaim } from './actions'

type Props = {
  playerId: number
  hideAlts: boolean
  hideEncounters: boolean
  hideEntirely: boolean
}

export function PrivacyToggles({ playerId, hideAlts, hideEncounters, hideEntirely }: Props) {
  const [pending, startTransition] = useTransition()

  function toggle(field: 'hideAlts' | 'hideEncounters' | 'hideEntirely', current: boolean) {
    if (field === 'hideEntirely' && !current) {
      if (!confirm('This hides the character from everyone except you and admins. Continue?')) return
    }
    startTransition(async () => { await setPrivacy(playerId, field, !current) })
  }

  function onUnclaim() {
    if (!confirm("You'll need to re-verify with a new bio code if you want to claim again. Unclaim?")) return
    startTransition(async () => { await unclaim(playerId) })
  }

  return (
    <div className="flex flex-wrap gap-3 text-xs">
      <Checkbox label="Hide alts" checked={hideAlts} disabled={pending} onChange={() => toggle('hideAlts', hideAlts)} />
      <Checkbox label="Hide encounters" checked={hideEncounters} disabled={pending} onChange={() => toggle('hideEncounters', hideEncounters)} />
      <Checkbox label="Hide entirely" checked={hideEntirely} disabled={pending} onChange={() => toggle('hideEntirely', hideEntirely)} />
      <button onClick={onUnclaim} disabled={pending} className="text-[var(--color-danger)] hover:underline">Unclaim</button>
    </div>
  )
}

function Checkbox({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: () => void }) {
  return (
    <label className="flex items-center gap-1.5 cursor-pointer">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={onChange} className="accent-[var(--color-gold)]" />
      <span className="text-[var(--color-text-muted)]">{label}</span>
    </label>
  )
}

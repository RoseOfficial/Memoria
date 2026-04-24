'use client'

import { useState, useTransition } from 'react'
import { approveTakedown, rejectTakedown } from './actions'
import type { TakedownListItem } from '../../../lib/types'

export function TakedownRow({ row }: { row: TakedownListItem }) {
  const [mode, setMode] = useState<'idle' | 'rejecting'>('idle')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, startTransition] = useTransition()

  function onApprove() {
    if (!confirm(`Approve takedown for ${row.nameSlug}@${row.worldSlug}? This sets HideEntirely.`)) return
    startTransition(async () => {
      const result = await approveTakedown(row.id, notes || null)
      if ('error' in result) setError(result.error)
    })
  }

  function onReject() {
    if (!notes.trim()) { setError('Add resolution notes before rejecting.'); return }
    startTransition(async () => {
      const result = await rejectTakedown(row.id, notes)
      if ('error' in result) setError(result.error)
    })
  }

  return (
    <div className="border border-[var(--color-bg-elevated)] p-6 space-y-3">
      <div className="flex justify-between">
        <div>
          <a href={`/p/${row.worldSlug}/${row.nameSlug}`} target="_blank" className="text-[var(--color-cream)] hover:text-[var(--color-gold)]">
            {row.nameSlug}@{row.worldSlug}
          </a>
          <p className="text-xs text-[var(--color-text-dim)]">
            Submitted {new Date(row.submittedAt).toLocaleString()} &middot; {row.contactEmail}
          </p>
        </div>
        <div className="flex gap-2">
          <button onClick={onApprove} disabled={pending} className="bg-[var(--color-gold)] text-[var(--color-bg)] px-4 py-2 text-sm font-semibold">
            Approve &amp; Hide
          </button>
          <button onClick={() => setMode(mode === 'rejecting' ? 'idle' : 'rejecting')} className="border border-[var(--color-bg-elevated)] px-4 py-2 text-sm">
            Reject
          </button>
        </div>
      </div>
      <p className="text-sm text-[var(--color-text-muted)] whitespace-pre-wrap">{row.reason}</p>
      {mode === 'rejecting' && (
        <div className="space-y-2">
          <textarea
            placeholder="Resolution notes (required for reject)"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            className="w-full bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] p-2 text-sm"
          />
          <button onClick={onReject} disabled={pending} className="bg-[var(--color-danger)] text-white px-4 py-2 text-sm">
            Confirm reject
          </button>
        </div>
      )}
      {error && <p className="text-[var(--color-danger)] text-sm">{error}</p>}
    </div>
  )
}

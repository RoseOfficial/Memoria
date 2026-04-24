'use client'

import { useState, useTransition } from 'react'
import { startClaim, verifyClaim } from '../../app/me/characters/actions'
import { toSlug } from '../../lib/slug'

export function ClaimModal({ onClose }: { onClose: () => void }) {
  const [step, setStep] = useState<'enter' | 'verify'>('enter')
  const [world, setWorld] = useState('')
  const [name, setName] = useState('')
  const [code, setCode] = useState<string | null>(null)
  const [playerId, setPlayerId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attemptsLeft, setAttemptsLeft] = useState<number | null>(null)
  const [pending, startTransition] = useTransition()

  function onStart(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setError(null)
    startTransition(async () => {
      const result = await startClaim(toSlug(world), toSlug(name))
      if ('error' in result) { setError(result.error); return }
      setCode(result.code)
      setPlayerId(result.playerId)
      setStep('verify')
    })
  }

  function onVerify() {
    if (playerId == null) return
    setError(null)
    startTransition(async () => {
      const result = await verifyClaim(playerId)
      if ('ok' in result) onClose()
      else { setError(result.error); setAttemptsLeft(result.attemptsLeft ?? null) }
    })
  }

  return (
    <div className="fixed inset-0 bg-black/80 flex items-center justify-center z-50" onClick={onClose}>
      <div className="bg-[var(--color-bg-raised)] border border-[var(--color-gold)] p-8 max-w-lg w-full mx-4 space-y-4" onClick={(e) => e.stopPropagation()}>
        {step === 'enter' ? (
          <form onSubmit={onStart} className="space-y-4">
            <h2 className="text-2xl">Claim a character</h2>
            <input required placeholder="World (e.g. Balmung)" value={world} onChange={(e) => setWorld(e.target.value)}
              className="w-full bg-[var(--color-bg)] border border-[var(--color-bg-elevated)] px-4 py-2" />
            <input required placeholder="Character name" value={name} onChange={(e) => setName(e.target.value)}
              className="w-full bg-[var(--color-bg)] border border-[var(--color-bg-elevated)] px-4 py-2" />
            {error && <p className="text-[var(--color-danger)] text-sm">{error}</p>}
            <div className="flex gap-2">
              <button type="submit" disabled={pending} className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-2 font-semibold">
                {pending ? 'Looking up…' : 'Continue'}
              </button>
              <button type="button" onClick={onClose} className="border border-[var(--color-bg-elevated)] px-6 py-2">Cancel</button>
            </div>
          </form>
        ) : (
          <div className="space-y-4">
            <h2 className="text-2xl">Verify ownership</h2>
            <p className="text-[var(--color-text-muted)] text-sm">
              Paste this code into your Lodestone character bio, save, then come back and click Verify.
              Codes expire in 30 minutes.
            </p>
            <code className="block bg-[var(--color-bg)] border border-[var(--color-gold)] p-4 text-center text-xl font-mono tracking-widest">
              {code}
            </code>
            {error && <p className="text-[var(--color-danger)] text-sm">
              {error}{attemptsLeft != null ? ` (${attemptsLeft} attempts left)` : ''}
            </p>}
            <div className="flex gap-2">
              <button onClick={onVerify} disabled={pending} className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-2 font-semibold">
                {pending ? 'Verifying…' : 'Verify'}
              </button>
              <button onClick={onClose} className="border border-[var(--color-bg-elevated)] px-6 py-2">Cancel</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

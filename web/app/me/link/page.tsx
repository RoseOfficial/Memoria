'use client'

import { useState, useTransition } from 'react'
import { redeemLinkCode } from './actions'

export default function LinkPage() {
  const [error, setError] = useState<string | null>(null)
  const [pending, startTransition] = useTransition()

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setError(null)
    const formData = new FormData(e.currentTarget)
    startTransition(async () => {
      const result = await redeemLinkCode(formData)
      if (result && 'error' in result) setError(result.error)
    })
  }

  return (
    <main className="max-w-xl mx-auto px-8 py-12 space-y-6">
      <h1 className="text-3xl">Redeem plugin link code</h1>
      <p className="text-[var(--color-text-muted)]">
        In the plugin, go to <em>Settings</em>, click <em>Generate web link code</em>, and paste the result here.
      </p>
      <form onSubmit={onSubmit} className="space-y-4">
        <input
          type="text"
          name="code"
          placeholder="AL-XXXX-XXXX"
          required
          pattern="AL-[A-Z0-9]{4}-[A-Z0-9]{4}"
          className="w-full font-mono bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] px-4 py-3 text-[var(--color-cream)] uppercase tracking-widest focus:border-[var(--color-gold)] focus:outline-none"
        />
        {error && <p className="text-[var(--color-danger)] text-sm">{error}</p>}
        <button
          type="submit"
          disabled={pending}
          className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-3 font-semibold hover:bg-[var(--color-gold-bright)] disabled:opacity-50"
        >
          {pending ? 'Redeeming…' : 'Redeem'}
        </button>
      </form>
    </main>
  )
}

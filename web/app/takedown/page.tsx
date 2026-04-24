'use client'

import { useState, useTransition } from 'react'
import { submitTakedown } from './actions'

export default function TakedownPage() {
  const [state, setState] = useState<'idle' | 'submitted'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [pending, startTransition] = useTransition()

  function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setError(null)
    const formData = new FormData(e.currentTarget)
    startTransition(async () => {
      const result = await submitTakedown(formData)
      if ('error' in result) setError(result.error)
      else setState('submitted')
    })
  }

  if (state === 'submitted') {
    return (
      <main className="max-w-xl mx-auto px-8 py-16 text-center space-y-4">
        <h1 className="text-3xl">Request received</h1>
        <p className="text-[var(--color-text-muted)]">
          We&apos;ll respond to the email you provided within 7 days.
        </p>
      </main>
    )
  }

  return (
    <main className="max-w-xl mx-auto px-8 py-12 space-y-6">
      <h1 className="text-3xl">Takedown request</h1>
      <p className="text-[var(--color-text-muted)]">
        Submit a takedown request to have a character hidden from Memoria. We review each
        request manually. Approved takedowns hide the character entirely for all visitors.
      </p>
      <form onSubmit={onSubmit} className="space-y-4">
        <FormField name="world" label="World (e.g. Balmung)" required />
        <FormField name="name" label="Character name" required />
        <FormField name="email" label="Your email (for response)" type="email" required />
        <div>
          <label className="block text-xs uppercase tracking-widest text-[var(--color-gold)] mb-1">
            Why are you requesting takedown?
          </label>
          <textarea name="reason" required maxLength={1000} rows={5}
            className="w-full bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] px-4 py-2 focus:border-[var(--color-gold)] focus:outline-none" />
        </div>
        {error && <p className="text-[var(--color-danger)] text-sm">{error}</p>}
        <button type="submit" disabled={pending} className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-3 font-semibold disabled:opacity-50">
          {pending ? 'Submitting…' : 'Submit request'}
        </button>
      </form>
    </main>
  )
}

function FormField({ name, label, type = 'text', required }: { name: string; label: string; type?: string; required?: boolean }) {
  return (
    <div>
      <label className="block text-xs uppercase tracking-widest text-[var(--color-gold)] mb-1">{label}</label>
      <input type={type} name={name} required={required}
        className="w-full bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] px-4 py-2 focus:border-[var(--color-gold)] focus:outline-none" />
    </div>
  )
}

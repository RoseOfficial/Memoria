'use client'

import { useRouter } from 'next/navigation'
import { useState } from 'react'

export function SearchBox({ autoFocus = false }: { autoFocus?: boolean }) {
  const [q, setQ] = useState('')
  const router = useRouter()
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        if (q.trim()) router.push(`/search?q=${encodeURIComponent(q.trim())}`)
      }}
      className="flex gap-2"
    >
      <input
        type="text"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Search player by name…"
        autoFocus={autoFocus}
        className="flex-1 bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] px-4 py-3 text-[var(--color-cream)] placeholder:text-[var(--color-text-dim)] focus:border-[var(--color-gold)] focus:outline-none"
      />
      <button
        type="submit"
        className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-3 font-semibold hover:bg-[var(--color-gold-bright)]"
      >
        Search
      </button>
    </form>
  )
}

'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState } from 'react'
import { toSlug } from '../../lib/slug'

type Suggestion = {
  localContentId: number
  name: string
  worldSlug: string
  worldName: string
  avatarUrl: string | null
}

const TYPEAHEAD_MIN_CHARS = 2
const TYPEAHEAD_DEBOUNCE_MS = 200
const TYPEAHEAD_LIMIT = 10

export function SearchBox({ autoFocus = false }: { autoFocus?: boolean }) {
  const [q, setQ] = useState('')
  const [suggestions, setSuggestions] = useState<Suggestion[]>([])
  const [highlight, setHighlight] = useState(-1)
  const [isOpen, setIsOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const router = useRouter()
  // Delayed blur so a mousedown on a suggestion can register before the
  // dropdown disappears underneath the cursor.
  const blurTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const trimmed = q.trim()
    if (trimmed.length < TYPEAHEAD_MIN_CHARS) {
      setSuggestions([])
      setIsLoading(false)
      return
    }
    setIsLoading(true)
    const controller = new AbortController()
    const timer = setTimeout(async () => {
      try {
        const res = await fetch(
          `/v1/players/search?q=${encodeURIComponent(trimmed)}&limit=${TYPEAHEAD_LIMIT}`,
          { signal: controller.signal },
        )
        if (!res.ok) return
        const data = (await res.json()) as { items: Suggestion[] }
        setSuggestions(data.items ?? [])
        setHighlight(-1)
      } catch (e) {
        if ((e as Error).name === 'AbortError') return
      } finally {
        setIsLoading(false)
      }
    }, TYPEAHEAD_DEBOUNCE_MS)
    return () => {
      clearTimeout(timer)
      // Cancels in-flight fetches so a stale earlier request can't overwrite
      // the suggestions for a later, more specific query.
      controller.abort()
    }
  }, [q])

  function navigateToResult(s: Suggestion) {
    router.push(`/p/${s.worldSlug}/${toSlug(s.name)}`)
    setIsOpen(false)
  }

  function onKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!isOpen || suggestions.length === 0) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlight((h) => (h + 1) % suggestions.length)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => (h <= 0 ? suggestions.length - 1 : h - 1))
    } else if (e.key === 'Enter' && highlight >= 0) {
      e.preventDefault()
      navigateToResult(suggestions[highlight])
    } else if (e.key === 'Escape') {
      setIsOpen(false)
    }
  }

  const showDropdown = isOpen && q.trim().length >= TYPEAHEAD_MIN_CHARS

  return (
    <div className="relative">
      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (q.trim()) {
            router.push(`/search?q=${encodeURIComponent(q.trim())}`)
            setIsOpen(false)
          }
        }}
        className="flex gap-2"
      >
        <input
          type="text"
          value={q}
          onChange={(e) => {
            setQ(e.target.value)
            setIsOpen(true)
          }}
          onFocus={() => setIsOpen(true)}
          onBlur={() => {
            blurTimer.current = setTimeout(() => setIsOpen(false), 150)
          }}
          onKeyDown={onKeyDown}
          placeholder="Search player by name…"
          autoFocus={autoFocus}
          className="flex-1 bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] px-4 py-3 text-[var(--color-cream)] placeholder:text-[var(--color-text-dim)] focus:border-[var(--color-gold)] focus:outline-none"
          autoComplete="off"
          aria-autocomplete="list"
          aria-expanded={showDropdown}
        />
        <button
          type="submit"
          className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-3 font-semibold hover:bg-[var(--color-gold-bright)]"
        >
          Search
        </button>
      </form>

      {showDropdown && (
        <ul
          role="listbox"
          className="absolute left-0 right-0 mt-1 bg-[var(--color-bg-raised)] border border-[var(--color-bg-elevated)] z-10 max-h-80 overflow-y-auto"
        >
          {isLoading && suggestions.length === 0 && (
            <li className="px-4 py-2 text-sm text-[var(--color-text-dim)]">Searching…</li>
          )}
          {!isLoading && suggestions.length === 0 && (
            <li className="px-4 py-2 text-sm text-[var(--color-text-dim)]">No matches.</li>
          )}
          {suggestions.map((s, i) => (
            <li
              key={s.localContentId}
              role="option"
              aria-selected={i === highlight}
              onMouseDown={(e) => {
                // preventDefault keeps the input focused so blur doesn't fire
                // before the click handler completes the navigation.
                e.preventDefault()
                if (blurTimer.current) clearTimeout(blurTimer.current)
                navigateToResult(s)
              }}
              onMouseEnter={() => setHighlight(i)}
              className={`px-4 py-2 text-sm cursor-pointer flex justify-between ${
                i === highlight
                  ? 'bg-[var(--color-bg-elevated)] text-[var(--color-gold)]'
                  : 'text-[var(--color-cream)]'
              }`}
            >
              <span>{s.name}</span>
              <span className="text-[var(--color-text-muted)]">{s.worldName}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

'use client'

export default function Error({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="flex flex-col items-center justify-center min-h-[60vh] gap-4 p-8">
      <h1 className="text-2xl">Something went wrong</h1>
      <p className="text-[var(--color-text-muted)]">An unexpected error occurred.</p>
      <button onClick={reset} className="border border-[var(--color-gold)] px-6 py-2 hover:bg-[var(--color-bg-raised)]">
        Retry
      </button>
    </main>
  )
}

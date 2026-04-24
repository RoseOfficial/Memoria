import Link from 'next/link'

export default function NotFound() {
  return (
    <main className="flex flex-col items-center justify-center min-h-[60vh] gap-4 p-8 text-center">
      <h1 className="text-3xl">Not found</h1>
      <p className="text-[var(--color-text-muted)] max-w-md">
        We can&apos;t find that page. If you were searching for a player, they may not have been scanned yet,
        or the name may be misspelled.
      </p>
      <Link href="/" className="text-[var(--color-gold)] hover:text-[var(--color-gold-bright)]">
        ← Back home
      </Link>
    </main>
  )
}

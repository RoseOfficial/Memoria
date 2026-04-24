import Link from 'next/link'

export function Footer() {
  return (
    <footer className="border-t border-[var(--color-bg-elevated)] px-6 py-6 mt-auto text-sm text-[var(--color-text-dim)]">
      <div className="flex flex-wrap gap-6">
        <Link href="/privacy">Privacy</Link>
        <Link href="/terms">Terms</Link>
        <Link href="/takedown">Takedown Request</Link>
        <span className="ml-auto">Memoria — FFXIV player lookup.</span>
      </div>
    </footer>
  )
}

import Link from 'next/link'

export function Nav() {
  return (
    <header className="border-b border-[var(--color-bg-elevated)] px-6 py-4 flex items-center gap-8">
      <Link href="/" className="font-[var(--font-display)] text-xl text-[var(--color-cream)] tracking-wider">
        AlphaScope
      </Link>
      <nav className="flex gap-6 text-sm text-[var(--color-text-muted)]">
        <Link href="/search">Search</Link>
        <Link href="/about">About</Link>
      </nav>
      <div className="ml-auto flex gap-4 text-sm">
        <Link href="/me" className="text-[var(--color-gold)]">Me</Link>
      </div>
    </header>
  )
}

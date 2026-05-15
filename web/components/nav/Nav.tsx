import Link from 'next/link'
import { getMe } from '../../lib/auth'

export async function Nav() {
  // getMe reads the session cookie via apiFetch and returns null when the user
  // isn't signed in. Treat any thrown error (transient API failure, server cold
  // start) as "not signed in" rather than letting it break the whole layout.
  const me = await getMe().catch(() => null)

  return (
    <header className="border-b border-[var(--color-bg-elevated)] px-6 py-4 flex items-center gap-8">
      <Link href="/" className="font-[var(--font-display)] text-xl text-[var(--color-cream)] tracking-wider">
        Memoria
      </Link>
      <nav className="flex gap-6 text-sm text-[var(--color-text-muted)]">
        <Link href="/search">Search</Link>
        <Link href="/about">About</Link>
      </nav>
      <div className="ml-auto flex items-center gap-4 text-sm">
        {me ? (
          <>
            <span className="text-[var(--color-text-muted)]">
              Signed in as <span className="text-[var(--color-cream)]">{me.name}</span>
            </span>
            <Link href="/me" className="text-[var(--color-gold)]">Me</Link>
          </>
        ) : (
          <Link href="/auth/signin" className="text-[var(--color-gold)]">Sign in</Link>
        )}
      </div>
    </header>
  )
}

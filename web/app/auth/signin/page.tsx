import { headers } from 'next/headers'
import { apiBaseUrl } from '../../../lib/env'
import { SignatureFrame } from '../../../components/ornaments/SignatureFrame'

export default async function SignIn({ searchParams }: { searchParams: Promise<{ return_to?: string }> }) {
  const params = await searchParams
  const h = await headers()
  // Server's IsAllowedReturnTo requires an absolute URL whose host is in the CORS allowlist;
  // a relative `/me` is rejected outright. Build it from the incoming request headers so
  // the same code works for production, preview, and local dev without an env var.
  const proto = h.get('x-forwarded-proto') ?? 'https'
  const host = h.get('host') ?? 'localhost'
  const returnPath = params.return_to ?? '/me'
  const returnTo = returnPath.startsWith('http') ? returnPath : `${proto}://${host}${returnPath}`
  const startUrl = `${apiBaseUrl()}/v1/auth/discord/start?return_to=${encodeURIComponent(returnTo)}`

  return (
    <main className="max-w-lg mx-auto px-8 py-20">
      <SignatureFrame className="py-12 px-8 text-center space-y-6">
        <h1 className="text-3xl">Sign in</h1>
        <p className="text-[var(--color-text-muted)]">
          Memoria uses Discord for sign-in. You must be a member of our Discord to unlock Tier 2 content.
        </p>
        <a
          href={startUrl}
          className="inline-block bg-[#5865f2] text-white px-6 py-3 font-semibold hover:opacity-90"
        >
          Continue with Discord →
        </a>
        <p className="text-xs text-[var(--color-text-muted)] italic">
          If Discord asks you to confirm in your Discord app, approve it there or click
          <span className="not-italic"> Continue to Discord </span>
          on the page that appears.
        </p>
      </SignatureFrame>
    </main>
  )
}

import { apiBaseUrl } from '../../../lib/env'
import { SignatureFrame } from '../../../components/ornaments/SignatureFrame'

export default async function SignIn({ searchParams }: { searchParams: Promise<{ return_to?: string }> }) {
  const params = await searchParams
  const returnTo = params.return_to ?? '/me'
  const startUrl = `${apiBaseUrl()}/v1/auth/discord/start?return_to=${encodeURIComponent(returnTo)}`

  return (
    <main className="max-w-lg mx-auto px-8 py-20">
      <SignatureFrame className="py-12 px-8 text-center space-y-6">
        <h1 className="text-3xl">Sign in</h1>
        <p className="text-[var(--color-text-muted)]">
          AlphaScope uses Discord for sign-in. You must be a member of our Discord to unlock Tier 2 content.
        </p>
        <a
          href={startUrl}
          className="inline-block bg-[#5865f2] text-white px-6 py-3 font-semibold hover:opacity-90"
        >
          Continue with Discord →
        </a>
      </SignatureFrame>
    </main>
  )
}

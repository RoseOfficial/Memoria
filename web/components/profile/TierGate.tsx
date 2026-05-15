import { SectionCard } from './JobsSection'
import Link from 'next/link'
import { getMe } from '../../lib/auth'

export async function TierGate({
  title,
  tier,
  sectionName,
  returnTo,
}: {
  title: string
  tier: 2 | 3
  sectionName: string
  // Path the sign-in flow should send the viewer back to. Caller passes the
  // current page so the user lands where they were rather than at /me.
  returnTo?: string
}) {
  // getMe is React.cache'd, so multiple TierGates on the same page share one
  // /v1/users/me call. Treat thrown errors as "not signed in" rather than
  // breaking the whole page render.
  const me = await getMe().catch(() => null)
  const signInHref = returnTo
    ? `/auth/signin?return_to=${encodeURIComponent(returnTo)}`
    : '/auth/signin'

  let copy: string
  let cta: React.ReactNode = null

  if (tier === 3) {
    copy = `You can only manage characters you've claimed.`
  } else if (!me) {
    copy = `Sign in with Discord to see ${sectionName}.`
    cta = <Link href={signInHref} className="text-[var(--color-gold)] text-sm">Sign in →</Link>
  } else {
    // Signed in, but tier still 1 — either not in the Memoria Discord guild,
    // or the cached membership check is older than 24h. The fix in both cases
    // is "join the Discord (if you haven't) and sign back in to refresh."
    copy = `${sectionName.charAt(0).toUpperCase() + sectionName.slice(1)} are visible to members of the Memoria Discord. If you've just joined, sign out and back in to refresh your guild status.`
    cta = (
      <Link href="/auth/signin" className="text-[var(--color-gold)] text-sm">
        Refresh sign-in →
      </Link>
    )
  }

  return (
    <SectionCard title={title}>
      <p className="text-sm text-[var(--color-text-muted)] mb-2">{copy}</p>
      {cta}
    </SectionCard>
  )
}

import { redirect, notFound } from 'next/navigation'
import { apiFetch } from '../../../../lib/api'
import type { PlayerProfileResponse } from '../../../../lib/types'

export default async function ProfilePage({ params }: { params: Promise<{ world: string; name: string }> }) {
  const { world, name } = await params
  const res = await apiFetch(`/v1/players/by-slug?world=${encodeURIComponent(world)}&name=${encodeURIComponent(name)}`, {
    redirect: 'manual',
  })

  if (res.status === 301) {
    const loc = res.headers.get('Location')
    if (loc) redirect(loc)
    notFound()
  }
  if (res.status === 404) notFound()
  if (!res.ok) throw new Error(`profile fetch failed: ${res.status}`)

  const profile = (await res.json()) as PlayerProfileResponse

  return (
    <main className="max-w-5xl mx-auto px-8 py-12">
      <pre className="text-xs text-[var(--color-text-dim)]">{JSON.stringify(profile.header, null, 2)}</pre>
      <p className="text-[var(--color-text-muted)] mt-4">Sections land in next tasks.</p>
    </main>
  )
}

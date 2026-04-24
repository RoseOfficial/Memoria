import { redirect, notFound } from 'next/navigation'
import { apiFetch } from '../../../../lib/api'
import type { PlayerProfileResponse } from '../../../../lib/types'
import { ProfileHeaderCard } from '../../../../components/profile/ProfileHeader'
import { JobsSection } from '../../../../components/profile/JobsSection'
import { CustomizationSection } from '../../../../components/profile/CustomizationSection'

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
    <main className="max-w-5xl mx-auto px-8 py-12 space-y-6">
      <ProfileHeaderCard header={profile.header} />
      <div className="grid md:grid-cols-2 gap-1 bg-[var(--color-bg-elevated)]">
        <JobsSection data={profile.jobs} />
        {profile.customization && <CustomizationSection data={profile.customization} />}
      </div>
    </main>
  )
}

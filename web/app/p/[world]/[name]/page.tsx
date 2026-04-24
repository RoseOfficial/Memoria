import { redirect, notFound } from 'next/navigation'
import { apiFetch } from '../../../../lib/api'
import type { PlayerProfileResponse } from '../../../../lib/types'
import { ProfileHeaderCard } from '../../../../components/profile/ProfileHeader'
import { JobsSection } from '../../../../components/profile/JobsSection'
import { CustomizationSection } from '../../../../components/profile/CustomizationSection'
import { MountsSection } from '../../../../components/profile/MountsSection'
import { MinionsSection } from '../../../../components/profile/MinionsSection'
import { WipSection } from '../../../../components/profile/WipSection'
import { TierGate } from '../../../../components/profile/TierGate'
import { LocationsSection } from '../../../../components/profile/LocationsSection'
import { HistorySection } from '../../../../components/profile/HistorySection'
import { AltsSection } from '../../../../components/profile/AltsSection'

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
        {profile.customization ? <CustomizationSection data={profile.customization} /> : <WipSection title="Customization" phase="Phase 1" description="capture missing customization bytes (hair, eyes, lips, face paint)" />}
        {profile.mounts ? <MountsSection data={profile.mounts} /> : <WipSection title="Mounts" phase="Phase 1" description="capture mount list from Lodestone" />}
        {profile.minions ? <MinionsSection data={profile.minions} /> : <WipSection title="Minions" phase="Phase 1" description="capture minion list from Lodestone" />}
        <WipSection title="Equipment / Glamour" phase="Phase 2" description="scan visible gear + dye channels for 14 equipment slots" />
        <WipSection title="Lodestone Bio" phase="Phase 3" description="add bio, guardian, nameday, city-state, GC + rank" />
        <WipSection title="Free Company" phase="Phase 3" description="fetch FC profile + member roster" />
        <WipSection title="Achievements / Collectibles" phase="Phase 4" description="add orchestrions, emotes, hairstyles, bardings, and more" />
        {profile.locations ? <LocationsSection data={profile.locations} /> : <TierGate title="Recent Locations" tier={2} sectionName="recent locations" />}
        {(profile.nameHistory !== null || profile.worldHistory !== null) ? <HistorySection names={profile.nameHistory} worlds={profile.worldHistory} /> : <TierGate title="Name / World History" tier={2} sectionName="name + world history" />}
        {profile.alts ? <AltsSection alts={profile.alts} /> : <TierGate title="Alt Characters" tier={2} sectionName="alt characters" />}
        <WipSection title="Encounter Network" phase="Phase 5" description="build social graph from co-territory observations + CWLS membership" />
      </div>
    </main>
  )
}

// Mirrors AlphaScopeServer/Models/DTOs/PlayerProfileResponse.cs
export type ProfileHeader = {
  localContentId: number
  name: string
  worldSlug: string
  worldName: string
  avatarUrl: string | null
  currentJobId: number | null
  currentJobLevel: number | null
  freeCompanyTag: string | null
  lastSeenAt: string | null
  lastSeenTerritory: string | null
  firstScannedAt: string | null
}

export type JobsData = { jobs: { name: string; level: number }[] }
export type CustomizationData = {
  bodyType: number | null; genderRace: number | null; height: number | null; face: number | null
  skinColor: number | null; nose: number | null; jaw: number | null; eyeShape: number | null
}
export type CollectibleIcon = { id: number; name: string; iconUrl: string }
export type MountsData = { collected: number; knownTotal: number; preview: CollectibleIcon[] }
export type MinionsData = { collected: number; knownTotal: number; preview: CollectibleIcon[] }
export type TerritoryEntry = { territoryId: number; territoryName: string; visitCount: number; lastVisitedAt: string }
export type LocationsData = { top: TerritoryEntry[] }
export type NameHistoryEntry = { name: string; changedAt: string }
export type WorldHistoryEntry = { worldSlug: string; worldName: string; changedAt: string }
export type AltCharacter = { name: string; worldSlug: string; worldName: string; localContentId: number }

export type PlayerProfileResponse = {
  header: ProfileHeader
  jobs: JobsData
  customization: CustomizationData | null
  mounts: MountsData | null
  minions: MinionsData | null
  locations: LocationsData | null
  nameHistory: NameHistoryEntry[] | null
  worldHistory: WorldHistoryEntry[] | null
  alts: AltCharacter[] | null
  isOwner: boolean
}

export type RecentPlayerItem = {
  name: string; worldSlug: string; worldName: string; avatarUrl: string | null; lastSeenAt: string
}
export type RecentPlayerResponse = { items: RecentPlayerItem[] }

export type ContributionsResponse = {
  lifetime: number
  recent: { playerName: string; worldSlug: string; worldName: string; scannedAt: string }[]
}

export type TakedownListItem = {
  id: number; worldSlug: string; nameSlug: string
  resolvedPlayerLocalContentId: number | null
  reason: string; contactEmail: string; submittedAt: string; status: string
}

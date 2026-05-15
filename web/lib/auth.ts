// Mirrors MemoriaServer/Models/DTOs/UserDto.cs
// The server serializes with Newtonsoft.Json [JsonProperty("N")] numeric-string keys.
// Wire parsing is confined to this file; all callers see clean-named types.
import { cache } from 'react'
import { apiFetch } from './api'

// ---------------------------------------------------------------------------
// Clean-named public types — used everywhere outside this file
// ---------------------------------------------------------------------------

export type MeResponse = {
  baseUrl: string
  gameAccountId: number
  localContentId: number
  name: string
  appRoleId: number
  characters: MeCharacter[]
  networkStats: MeNetworkStats
}

export type MeCharacter = {
  name: string | null
  localContentId: number | null
  avatarLink: string | null
  privacy: MeCharacterPrivacy | null
  visitInfo: MeCharacterVisitInfo | null
}

export type MeCharacterPrivacy = {
  hideFullProfile: boolean
  hideTerritoryInfo: boolean
  hideCustomizations: boolean
  hideInSearchResults: boolean
  hideRetainersInfo: boolean
  hideAltCharacters: boolean
}

export type MeCharacterVisitInfo = {
  profileTotalVisitCount: number | null
  lastProfileVisitDate: number | null  // unix seconds
}

export type MeNetworkStats = {
  uploadedPlayersCount: number
  uploadedPlayerInfoCount: number
  uploadedRetainersCount: number
  uploadedRetainerInfoCount: number
  fetchedPlayerInfoCount: number
  searchedNamesCount: number
  lastSyncedTime: number  // unix seconds
}

// ---------------------------------------------------------------------------
// Wire types — numeric-string keys as serialized by Newtonsoft.Json
// Kept private: nothing outside this file should reference these.
// ---------------------------------------------------------------------------

// MemoriaServer.Models.DTOs.User
type WireUser = {
  '0': string        // BaseUrl
  '1': number        // GameAccountId (non-nullable int on the C# side)
  '2': number        // LocalContentId (long, JS number is fine up to 2^53)
  '3': string        // Name
  '4': number        // AppRoleId
  '5': WireCharacter[]
  '6': WireNetworkStats
}

// MemoriaServer.Models.DTOs.UserCharacterDto
// Note: starts at key "1" — there is no "0" field on this DTO
type WireCharacter = {
  '1': string | null   // Name
  '2': number | null   // LocalContentId (long?)
  '3': WirePrivacy | null
  '4': WireVisitInfo | null
  '5': string | null   // AvatarLink
}

// MemoriaServer.Models.DTOs.CharacterPrivacySettingsDto
type WirePrivacy = {
  '1': boolean  // HideFullProfile
  '2': boolean  // HideTerritoryInfo
  '3': boolean  // HideCustomizations
  '4': boolean  // HideInSearchResults
  '5': boolean  // HideRetainersInfo
  '6': boolean  // HideAltCharacters
}

// MemoriaServer.Models.DTOs.CharacterProfileVisitInfoDto
type WireVisitInfo = {
  '1': number | null  // ProfileTotalVisitCount
  '2': number | null  // LastProfileVisitDate (unix seconds)
}

// MemoriaServer.Models.DTOs.UserNetworkStatsDto
type WireNetworkStats = {
  '1': number  // UploadedPlayersCount
  '2': number  // UploadedPlayerInfoCount
  '3': number  // UploadedRetainersCount
  '4': number  // UploadedRetainerInfoCount
  '5': number  // FetchedPlayerInfoCount
  '6': number  // SearchedNamesCount
  '7': number  // LastSyncedTime (unix seconds)
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

// React.cache memoizes per render pass so multiple server components in the
// same request (e.g. Nav + several TierGates on a profile page) only trigger
// one /v1/users/me round trip. apiFetch sets cache:'no-store' which disables
// Next's built-in fetch dedup, so we wrap explicitly here.
export const getMe = cache(async (): Promise<MeResponse | null> => {
  const res = await apiFetch('/v1/users/me')
  if (res.status === 401 || res.status === 404) return null
  if (!res.ok) throw new Error(`/v1/users/me returned ${res.status}`)
  const wire = (await res.json()) as WireUser
  return adaptMe(wire)
})

// ---------------------------------------------------------------------------
// Adapters — wire → clean
// ---------------------------------------------------------------------------

function adaptMe(w: WireUser): MeResponse {
  return {
    baseUrl: w['0'],
    gameAccountId: w['1'],
    localContentId: w['2'],
    name: w['3'],
    appRoleId: w['4'],
    characters: (w['5'] ?? []).map(adaptCharacter),
    networkStats: adaptNetworkStats(w['6']),
  }
}

function adaptCharacter(c: WireCharacter): MeCharacter {
  return {
    name: c['1'] ?? null,
    localContentId: c['2'] ?? null,
    privacy: c['3'] ? adaptPrivacy(c['3']) : null,
    visitInfo: c['4'] ? adaptVisitInfo(c['4']) : null,
    avatarLink: c['5'] ?? null,
  }
}

function adaptPrivacy(p: WirePrivacy): MeCharacterPrivacy {
  return {
    hideFullProfile: p['1'],
    hideTerritoryInfo: p['2'],
    hideCustomizations: p['3'],
    hideInSearchResults: p['4'],
    hideRetainersInfo: p['5'],
    hideAltCharacters: p['6'],
  }
}

function adaptVisitInfo(v: WireVisitInfo): MeCharacterVisitInfo {
  return {
    profileTotalVisitCount: v['1'] ?? null,
    lastProfileVisitDate: v['2'] ?? null,
  }
}

function adaptNetworkStats(s: WireNetworkStats): MeNetworkStats {
  return {
    uploadedPlayersCount: s['1'],
    uploadedPlayerInfoCount: s['2'],
    uploadedRetainersCount: s['3'],
    uploadedRetainerInfoCount: s['4'],
    fetchedPlayerInfoCount: s['5'],
    searchedNamesCount: s['6'],
    lastSyncedTime: s['7'],
  }
}

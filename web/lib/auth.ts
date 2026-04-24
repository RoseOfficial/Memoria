// Mirrors AlphaScopeServer/Models/DTOs/UserDto.cs (numeric JsonProperty keys are the wire keys)
import { apiFetch } from './api'

export type MeCharacterPrivacy = {
  '1': boolean  // HideFullProfile
  '2': boolean  // HideTerritoryInfo
  '3': boolean  // HideCustomizations
  '4': boolean  // HideInSearchResults
  '5': boolean  // HideRetainersInfo
  '6': boolean  // HideAltCharacters
}

export type MeCharacterVisitInfo = {
  '1': number | null  // ProfileTotalVisitCount
  '2': number | null  // LastProfileVisitDate (unix seconds)
}

export type MeCharacter = {
  '1': string | null   // Name
  '2': number | null   // LocalContentId
  '3': MeCharacterPrivacy | null
  '4': MeCharacterVisitInfo | null
  '5': string | null   // AvatarLink
}

export type MeNetworkStats = {
  '1': number  // UploadedPlayersCount
  '2': number  // UploadedPlayerInfoCount
  '3': number  // UploadedRetainersCount
  '4': number  // UploadedRetainerInfoCount
  '5': number  // FetchedPlayerInfoCount
  '6': number  // SearchedNamesCount
  '7': number  // LastSyncedTime (unix seconds)
}

export type MeResponse = {
  '0': string          // BaseUrl
  '1': number          // GameAccountId
  '2': number          // LocalContentId
  '3': string          // Name
  '4': number          // AppRoleId
  '5': MeCharacter[]  // Characters
  '6': MeNetworkStats  // NetworkStats
}

export async function getMe(): Promise<MeResponse | null> {
  const res = await apiFetch('/v1/users/me')
  if (res.status === 401 || res.status === 404) return null
  if (!res.ok) throw new Error(`/v1/users/me returned ${res.status}`)
  return res.json() as Promise<MeResponse>
}

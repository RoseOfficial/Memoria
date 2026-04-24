'use server'

import { apiFetch } from '../../../lib/api'
import { revalidatePath } from 'next/cache'

export async function startClaim(world: string, name: string): Promise<{ code: string; expiresAt: string; playerId: number } | { error: string }> {
  // Resolve slug → player id via by-slug endpoint (returns 200 with header.localContentId)
  const lookup = await apiFetch(`/v1/players/by-slug?world=${encodeURIComponent(world)}&name=${encodeURIComponent(name)}`, { redirect: 'manual' })
  if (lookup.status === 301) {
    const loc = lookup.headers.get('Location')
    if (loc) {
      // re-fetch at the canonical URL
      const m = loc.match(/\/p\/([^/]+)\/([^/]+)/)
      if (m) return startClaim(m[1], m[2])
    }
    return { error: 'Character moved to a new world or renamed; please retry with current details.' }
  }
  if (lookup.status === 404) return { error: 'No character found with that name on that world.' }
  if (!lookup.ok) return { error: `Lookup failed (${lookup.status}).` }

  const profile = await lookup.json()
  const playerId = profile.header.localContentId as number

  const startRes = await apiFetch(`/v1/players/${playerId}/claim/start`, { method: 'POST' })
  if (!startRes.ok) {
    const text = await startRes.text().catch(() => '')
    return { error: text || `Start failed (${startRes.status})` }
  }
  const body = await startRes.json()
  return { code: body.code, expiresAt: body.expiresAt, playerId }
}

export async function verifyClaim(playerId: number): Promise<{ ok: true } | { error: string; attemptsLeft?: number }> {
  const res = await apiFetch(`/v1/players/${playerId}/claim/verify`, { method: 'POST' })
  if (res.ok) {
    revalidatePath('/me/characters')
    return { ok: true }
  }
  const body = await res.json().catch(() => ({}))
  return { error: body.error ?? `Verify failed (${res.status})`, attemptsLeft: body.attemptsLeft }
}

export async function setPrivacy(playerId: number, field: 'hideAlts' | 'hideEncounters' | 'hideEntirely', value: boolean): Promise<{ ok: true } | { error: string }> {
  const body: Record<string, boolean> = {}
  body[field] = value
  const res = await apiFetch(`/v1/players/${playerId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) return { error: `Update failed (${res.status})` }
  revalidatePath('/me/characters')
  return { ok: true }
}

export async function unclaim(playerId: number): Promise<{ ok: true } | { error: string }> {
  const res = await apiFetch(`/v1/players/${playerId}/claim`, { method: 'DELETE' })
  if (!res.ok) return { error: `Unclaim failed (${res.status})` }
  revalidatePath('/me/characters')
  return { ok: true }
}

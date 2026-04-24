'use server'

import { apiFetch } from '../../lib/api'

export async function submitTakedown(formData: FormData): Promise<{ ok: true } | { error: string }> {
  const body = {
    worldSlug: String(formData.get('world') ?? '').trim().toLowerCase(),
    nameSlug: String(formData.get('name') ?? '').trim().toLowerCase().replace(/'/g, '').replace(/\s+/g, '-'),
    reason: String(formData.get('reason') ?? '').trim(),
    contactEmail: String(formData.get('email') ?? '').trim(),
  }

  if (!body.worldSlug || !body.nameSlug || !body.reason || !body.contactEmail) {
    return { error: 'All fields required.' }
  }
  if (body.reason.length > 1000) return { error: 'Reason too long (max 1000 chars).' }

  const res = await apiFetch('/v1/takedowns', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (res.status === 429) return { error: 'Too many requests. Try again in an hour.' }
  if (!res.ok) return { error: `Submission failed (${res.status}).` }
  return { ok: true }
}

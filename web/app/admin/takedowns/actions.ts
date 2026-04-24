'use server'

import { apiFetch } from '../../../lib/api'
import { revalidatePath } from 'next/cache'

export async function approveTakedown(id: number, notes: string | null): Promise<{ ok: true } | { error: string }> {
  const res = await apiFetch(`/v1/takedowns/${id}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ action: 'approve', notes }),
  })
  if (!res.ok) return { error: `Approve failed (${res.status})` }
  revalidatePath('/admin/takedowns')
  return { ok: true }
}

export async function rejectTakedown(id: number, notes: string): Promise<{ ok: true } | { error: string }> {
  const res = await apiFetch(`/v1/takedowns/${id}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ action: 'reject', notes }),
  })
  if (!res.ok) return { error: `Reject failed (${res.status})` }
  revalidatePath('/admin/takedowns')
  return { ok: true }
}

'use server'

import { apiFetch } from '../../../lib/api'
import { redirect } from 'next/navigation'

export async function redeemLinkCode(formData: FormData) {
  const code = String(formData.get('code') ?? '').trim()
  if (!/^AL-[A-Z0-9]{4}-[A-Z0-9]{4}$/i.test(code)) {
    return { error: 'Invalid code format. Expected AL-XXXX-XXXX.' }
  }

  const res = await apiFetch('/v1/auth/link/redeem', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  })

  if (!res.ok) {
    const text = await res.text().catch(() => '')
    return { error: text || `Server returned ${res.status}` }
  }

  redirect('/me')
}

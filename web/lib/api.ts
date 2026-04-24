import { cookies } from 'next/headers'
import { apiBaseUrl } from './env'

export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  const store = await cookies()
  const alpha = store.get('__Host-memoria')?.value
  const headers = new Headers(init?.headers)
  if (alpha) headers.set('Cookie', `__Host-memoria=${alpha}`)
  return fetch(`${apiBaseUrl()}${path}`, {
    ...init,
    headers,
    cache: init?.cache ?? 'no-store',
  })
}

export async function apiFetchJson<T>(path: string, init?: RequestInit): Promise<T | null> {
  const res = await apiFetch(path, init)
  if (res.status === 404) return null
  if (!res.ok) throw new Error(`API ${res.status} for ${path}`)
  return res.json() as Promise<T>
}

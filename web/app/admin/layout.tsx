import { notFound } from 'next/navigation'
import { apiFetchJson } from '../../lib/api'

type AdminCheck = { isAdmin: boolean }

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const check = await apiFetchJson<AdminCheck>('/v1/users/me/admin').catch(() => null)
  if (!check?.isAdmin) notFound()
  return <>{children}</>
}

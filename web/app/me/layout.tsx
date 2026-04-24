import { redirect } from 'next/navigation'
import { getMe } from '../../lib/auth'

export default async function MeLayout({ children }: { children: React.ReactNode }) {
  const me = await getMe()
  if (!me) redirect('/auth/signin?return_to=/me')
  return <>{children}</>
}

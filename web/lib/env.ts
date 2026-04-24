export function apiBaseUrl(): string {
  const v = process.env.NEXT_PUBLIC_API_BASE_URL
  if (!v || v.trim() === '') {
    throw new Error('NEXT_PUBLIC_API_BASE_URL is not set. Check .env.local.')
  }
  return v.replace(/\/+$/, '')
}

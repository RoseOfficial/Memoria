import { describe, expect, it, vi, beforeEach } from 'vitest'

// Mock next/headers before importing api
vi.mock('next/headers', () => ({
  cookies: () => ({ get: (name: string) => name === '__Host-alpha' ? { value: 'abc123' } : undefined }),
}))

beforeEach(() => {
  process.env.NEXT_PUBLIC_API_BASE_URL = 'https://api.test'
  globalThis.fetch = vi.fn(async () => new Response('{}', { status: 200 })) as any
})

describe('apiFetch', () => {
  it('prepends base URL', async () => {
    const { apiFetch } = await import('../../lib/api')
    await apiFetch('/v1/players/recent')
    const callArg = (globalThis.fetch as any).mock.calls[0][0]
    expect(callArg).toBe('https://api.test/v1/players/recent')
  })

  it('forwards __Host-alpha cookie as Cookie header', async () => {
    const { apiFetch } = await import('../../lib/api')
    await apiFetch('/v1/users/me')
    const init = (globalThis.fetch as any).mock.calls[0][1]
    const cookieHeader = (init.headers as Headers).get('Cookie')
    expect(cookieHeader).toBe('__Host-alpha=abc123')
  })

  it('defaults to no-store cache', async () => {
    const { apiFetch } = await import('../../lib/api')
    await apiFetch('/v1/players/recent')
    const init = (globalThis.fetch as any).mock.calls[0][1]
    expect(init.cache).toBe('no-store')
  })

  it('respects explicit cache option', async () => {
    const { apiFetch } = await import('../../lib/api')
    await apiFetch('/v1/players/recent', { cache: 'force-cache' })
    const init = (globalThis.fetch as any).mock.calls[0][1]
    expect(init.cache).toBe('force-cache')
  })
})

import { describe, expect, it, afterEach } from 'vitest'

describe('env', () => {
  const original = process.env.NEXT_PUBLIC_API_BASE_URL

  afterEach(() => {
    process.env.NEXT_PUBLIC_API_BASE_URL = original
  })

  it('returns the URL when set', async () => {
    process.env.NEXT_PUBLIC_API_BASE_URL = 'https://api.example.com'
    const { apiBaseUrl } = await import('../../lib/env')
    expect(apiBaseUrl()).toBe('https://api.example.com')
  })

  it('throws when not set', async () => {
    delete process.env.NEXT_PUBLIC_API_BASE_URL
    const { apiBaseUrl } = await import('../../lib/env')
    expect(() => apiBaseUrl()).toThrow(/NEXT_PUBLIC_API_BASE_URL/)
  })

  it('strips trailing slash', async () => {
    process.env.NEXT_PUBLIC_API_BASE_URL = 'https://api.example.com/'
    const { apiBaseUrl } = await import('../../lib/env')
    expect(apiBaseUrl()).toBe('https://api.example.com')
  })
})

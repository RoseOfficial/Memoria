import { describe, expect, it } from 'vitest'
import { toSlug } from '../../lib/slug'
import { DATA_CENTER_ORDER, WORLDS, WORLDS_BY_DC } from '../../lib/worlds'

describe('worlds list', () => {
  it('every entry has slug = toSlug(name)', () => {
    for (const w of WORLDS) {
      expect(w.slug).toBe(toSlug(w.name))
    }
  })

  it('all slugs are unique', () => {
    const slugs = WORLDS.map((w) => w.slug)
    expect(new Set(slugs).size).toBe(slugs.length)
  })

  it('all ids are unique', () => {
    const ids = WORLDS.map((w) => w.id)
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('every dataCenter is in the canonical order', () => {
    const allowed = new Set(DATA_CENTER_ORDER)
    for (const w of WORLDS) {
      expect(allowed.has(w.dataCenter)).toBe(true)
    }
  })

  it('groups every world into its data center bucket', () => {
    const total = DATA_CENTER_ORDER.reduce((sum, dc) => sum + WORLDS_BY_DC[dc].length, 0)
    expect(total).toBe(WORLDS.length)
  })

  it('worlds within each data center are sorted alphabetically by name', () => {
    for (const dc of DATA_CENTER_ORDER) {
      const names = WORLDS_BY_DC[dc].map((w) => w.name)
      const sorted = [...names].sort((a, b) => a.localeCompare(b))
      expect(names).toEqual(sorted)
    }
  })
})

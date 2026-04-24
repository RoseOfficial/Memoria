import { describe, expect, it } from 'vitest'
import { toSlug } from '../../lib/slug'

describe('toSlug', () => {
  it('lowercases', () => expect(toSlug('Balmung')).toBe('balmung'))
  it('strips apostrophes', () => expect(toSlug("T'chai")).toBe('tchai'))
  it('hyphenates spaces', () => expect(toSlug('Tataru Taru')).toBe('tataru-taru'))
  it('handles mixed', () => expect(toSlug("T'chai Nunh")).toBe('tchai-nunh'))
  it('collapses multiple spaces', () => expect(toSlug('A  B')).toBe('a-b'))
  it('trims leading/trailing spaces', () => expect(toSlug('  Balmung  ')).toBe('balmung'))
  it('returns empty for empty input', () => expect(toSlug('')).toBe(''))
})

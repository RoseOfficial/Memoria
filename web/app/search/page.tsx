import { apiFetchJson } from '../../lib/api'
import { SearchBox } from '../../components/forms/SearchBox'
import { toSlug } from '../../lib/slug'
import Link from 'next/link'

// Wire format from GET /v1/players uses short JsonProperty keys.
// PaginationBase<T>: { D: T[], L: number, N: number }
// PlayerSearchDto fields used here: N (name), W (worldId as short)
type SearchResultItem = { N: string; W: number | null }
type SearchResponse = { D: SearchResultItem[] }

// Inline world-id → name lookup matching AlphaScopeServer/Services/World/WorldNames.cs
const WORLD_NAMES: Record<number, string> = {
  // Aether (NA)
  34: 'Brynhildr', 62: 'Diabolos', 75: 'Malboro', 37: 'Mateus',
  73: 'Adamantoise', 79: 'Cactuar', 54: 'Faerie', 63: 'Gilgamesh',
  40: 'Jenova', 65: 'Midgardsormr', 99: 'Sargatanas', 57: 'Siren',
  // Primal (NA)
  53: 'Exodus', 78: 'Behemoth', 93: 'Excalibur', 35: 'Famfrit',
  95: 'Hyperion', 55: 'Lamia', 64: 'Leviathan', 77: 'Ultros',
  // Crystal (NA)
  91: 'Balmung', 81: 'Goblin', 41: 'Zalera', 74: 'Coeurl',
  // Chaos (EU)
  80: 'Cerberus', 71: 'Moogle', 39: 'Omega', 97: 'Ragnarok', 85: 'Spriggan',
  // Light (EU)
  36: 'Lich', 66: 'Odin', 56: 'Phoenix', 67: 'Shiva', 33: 'Twintania',
  // Elemental (JP)
  23: 'Asura', 45: 'Carbuncle', 58: 'Garuda', 59: 'Ifrit', 49: 'Kujata', 50: 'Typhon',
  // Gaia (JP)
  43: 'Alexander', 69: 'Bahamut', 92: 'Durandal', 46: 'Fenrir', 51: 'Ultima', 98: 'Ridill',
  // Mana (JP)
  44: 'Anima', 70: 'Chocobo', 47: 'Hades', 48: 'Ixion', 96: 'Masamune', 61: 'Titan', 28: 'Pandaemonium',
  // Meteor (JP)
  24: 'Belias', 82: 'Mandragora', 60: 'Ramuh', 29: 'Shinryu', 52: 'Valefor', 30: 'Unicorn', 31: 'Yojimbo', 32: 'Zeromus',
  // Materia (OCE)
  21: 'Ravana', 22: 'Bismarck', 86: 'Sephirot', 87: 'Sophia', 88: 'Zurvan',
}

function resolveWorld(worldId: number | null): { name: string; slug: string } {
  const name = (worldId != null && WORLD_NAMES[worldId]) || 'Unknown'
  return { name, slug: toSlug(name) }
}

export default async function SearchPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams
  const q = (params.q ?? '').trim()
  const results = q
    ? await apiFetchJson<SearchResponse>(
        `/v1/players?Name=${encodeURIComponent(q)}&F_MatchAnyPartOfName=true`
      ).catch(() => ({ D: [] }))
    : null

  const items = results?.D ?? []

  return (
    <main className="max-w-3xl mx-auto px-8 py-12 space-y-8">
      <h1 className="text-3xl">Search</h1>
      <SearchBox />
      {q && (
        <div>
          <p className="text-[var(--color-text-muted)] text-sm mb-4">
            {items.length} result{items.length === 1 ? '' : 's'} for &ldquo;{q}&rdquo;
          </p>
          <ul className="divide-y divide-[var(--color-bg-elevated)]">
            {items.map((r, i) => {
              const world = resolveWorld(r.W)
              return (
                <li key={i} className="py-3 flex justify-between text-sm">
                  <Link
                    href={`/p/${world.slug}/${toSlug(r.N)}`}
                    className="text-[var(--color-cream)] hover:text-[var(--color-gold)]"
                  >
                    {r.N}
                  </Link>
                  <span className="text-[var(--color-text-muted)]">{world.name}</span>
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </main>
  )
}

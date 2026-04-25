// Canonical FFXIV world list. Mirrors MemoriaServer/Services/World/WorldNames.cs;
// the parity test at MemoriaServer.Tests/Services/WorldsListParityTests.cs reads
// this file and asserts every server-side world appears here with the same id,
// name, and slug. If you add a world here, add it to WorldNames.Map (and vice
// versa) — drift breaks the by-slug lookup or makes worlds invisible in the UI.
//
// Format must stay simple-line per entry so the C# regex parser can pick them
// up: `{ id: <num>, name: '<str>', slug: '<str>', dataCenter: '<DC>' },`.

import { toSlug } from './slug'

export type DataCenter =
  | 'Aether'
  | 'Primal'
  | 'Crystal'
  | 'Chaos'
  | 'Light'
  | 'Elemental'
  | 'Gaia'
  | 'Mana'
  | 'Meteor'
  | 'Materia'

export interface World {
  id: number
  name: string
  slug: string
  dataCenter: DataCenter
}

export const WORLDS: World[] = [
  // Aether (NA)
  { id: 73, name: 'Adamantoise', slug: 'adamantoise', dataCenter: 'Aether' },
  { id: 34, name: 'Brynhildr', slug: 'brynhildr', dataCenter: 'Aether' },
  { id: 79, name: 'Cactuar', slug: 'cactuar', dataCenter: 'Aether' },
  { id: 62, name: 'Diabolos', slug: 'diabolos', dataCenter: 'Aether' },
  { id: 54, name: 'Faerie', slug: 'faerie', dataCenter: 'Aether' },
  { id: 63, name: 'Gilgamesh', slug: 'gilgamesh', dataCenter: 'Aether' },
  { id: 40, name: 'Jenova', slug: 'jenova', dataCenter: 'Aether' },
  { id: 75, name: 'Malboro', slug: 'malboro', dataCenter: 'Aether' },
  { id: 37, name: 'Mateus', slug: 'mateus', dataCenter: 'Aether' },
  { id: 65, name: 'Midgardsormr', slug: 'midgardsormr', dataCenter: 'Aether' },
  { id: 99, name: 'Sargatanas', slug: 'sargatanas', dataCenter: 'Aether' },
  { id: 57, name: 'Siren', slug: 'siren', dataCenter: 'Aether' },

  // Primal (NA)
  { id: 78, name: 'Behemoth', slug: 'behemoth', dataCenter: 'Primal' },
  { id: 93, name: 'Excalibur', slug: 'excalibur', dataCenter: 'Primal' },
  { id: 53, name: 'Exodus', slug: 'exodus', dataCenter: 'Primal' },
  { id: 35, name: 'Famfrit', slug: 'famfrit', dataCenter: 'Primal' },
  { id: 95, name: 'Hyperion', slug: 'hyperion', dataCenter: 'Primal' },
  { id: 55, name: 'Lamia', slug: 'lamia', dataCenter: 'Primal' },
  { id: 64, name: 'Leviathan', slug: 'leviathan', dataCenter: 'Primal' },
  { id: 77, name: 'Ultros', slug: 'ultros', dataCenter: 'Primal' },

  // Crystal (NA)
  { id: 91, name: 'Balmung', slug: 'balmung', dataCenter: 'Crystal' },
  { id: 74, name: 'Coeurl', slug: 'coeurl', dataCenter: 'Crystal' },
  { id: 81, name: 'Goblin', slug: 'goblin', dataCenter: 'Crystal' },
  { id: 41, name: 'Zalera', slug: 'zalera', dataCenter: 'Crystal' },

  // Chaos (EU)
  { id: 80, name: 'Cerberus', slug: 'cerberus', dataCenter: 'Chaos' },
  { id: 71, name: 'Moogle', slug: 'moogle', dataCenter: 'Chaos' },
  { id: 39, name: 'Omega', slug: 'omega', dataCenter: 'Chaos' },
  { id: 97, name: 'Ragnarok', slug: 'ragnarok', dataCenter: 'Chaos' },
  { id: 85, name: 'Spriggan', slug: 'spriggan', dataCenter: 'Chaos' },

  // Light (EU)
  { id: 36, name: 'Lich', slug: 'lich', dataCenter: 'Light' },
  { id: 66, name: 'Odin', slug: 'odin', dataCenter: 'Light' },
  { id: 56, name: 'Phoenix', slug: 'phoenix', dataCenter: 'Light' },
  { id: 67, name: 'Shiva', slug: 'shiva', dataCenter: 'Light' },
  { id: 33, name: 'Twintania', slug: 'twintania', dataCenter: 'Light' },

  // Elemental (JP)
  { id: 23, name: 'Asura', slug: 'asura', dataCenter: 'Elemental' },
  { id: 45, name: 'Carbuncle', slug: 'carbuncle', dataCenter: 'Elemental' },
  { id: 58, name: 'Garuda', slug: 'garuda', dataCenter: 'Elemental' },
  { id: 59, name: 'Ifrit', slug: 'ifrit', dataCenter: 'Elemental' },
  { id: 49, name: 'Kujata', slug: 'kujata', dataCenter: 'Elemental' },
  { id: 50, name: 'Typhon', slug: 'typhon', dataCenter: 'Elemental' },

  // Gaia (JP)
  { id: 43, name: 'Alexander', slug: 'alexander', dataCenter: 'Gaia' },
  { id: 69, name: 'Bahamut', slug: 'bahamut', dataCenter: 'Gaia' },
  { id: 92, name: 'Durandal', slug: 'durandal', dataCenter: 'Gaia' },
  { id: 46, name: 'Fenrir', slug: 'fenrir', dataCenter: 'Gaia' },
  { id: 98, name: 'Ridill', slug: 'ridill', dataCenter: 'Gaia' },
  { id: 51, name: 'Ultima', slug: 'ultima', dataCenter: 'Gaia' },

  // Mana (JP)
  { id: 44, name: 'Anima', slug: 'anima', dataCenter: 'Mana' },
  { id: 70, name: 'Chocobo', slug: 'chocobo', dataCenter: 'Mana' },
  { id: 47, name: 'Hades', slug: 'hades', dataCenter: 'Mana' },
  { id: 48, name: 'Ixion', slug: 'ixion', dataCenter: 'Mana' },
  { id: 96, name: 'Masamune', slug: 'masamune', dataCenter: 'Mana' },
  { id: 28, name: 'Pandaemonium', slug: 'pandaemonium', dataCenter: 'Mana' },
  { id: 61, name: 'Titan', slug: 'titan', dataCenter: 'Mana' },

  // Meteor (JP)
  { id: 24, name: 'Belias', slug: 'belias', dataCenter: 'Meteor' },
  { id: 82, name: 'Mandragora', slug: 'mandragora', dataCenter: 'Meteor' },
  { id: 60, name: 'Ramuh', slug: 'ramuh', dataCenter: 'Meteor' },
  { id: 29, name: 'Shinryu', slug: 'shinryu', dataCenter: 'Meteor' },
  { id: 30, name: 'Unicorn', slug: 'unicorn', dataCenter: 'Meteor' },
  { id: 52, name: 'Valefor', slug: 'valefor', dataCenter: 'Meteor' },
  { id: 31, name: 'Yojimbo', slug: 'yojimbo', dataCenter: 'Meteor' },
  { id: 32, name: 'Zeromus', slug: 'zeromus', dataCenter: 'Meteor' },

  // Materia (OCE)
  { id: 22, name: 'Bismarck', slug: 'bismarck', dataCenter: 'Materia' },
  { id: 21, name: 'Ravana', slug: 'ravana', dataCenter: 'Materia' },
  { id: 86, name: 'Sephirot', slug: 'sephirot', dataCenter: 'Materia' },
  { id: 87, name: 'Sophia', slug: 'sophia', dataCenter: 'Materia' },
  { id: 88, name: 'Zurvan', slug: 'zurvan', dataCenter: 'Materia' },
]

export const DATA_CENTER_ORDER: DataCenter[] = [
  'Aether',
  'Primal',
  'Crystal',
  'Chaos',
  'Light',
  'Elemental',
  'Gaia',
  'Mana',
  'Meteor',
  'Materia',
]

export const WORLDS_BY_DC: Record<DataCenter, World[]> = DATA_CENTER_ORDER.reduce(
  (acc, dc) => {
    acc[dc] = WORLDS.filter((w) => w.dataCenter === dc)
    return acc
  },
  {} as Record<DataCenter, World[]>,
)

// Sanity check at module load: every entry's slug must match toSlug(name). Catches
// hand-edits that forget to lowercase or hyphenate. Cheap to run; runs once per
// import. Throws to fail loud rather than silently breaking dropdown values.
for (const w of WORLDS) {
  if (w.slug !== toSlug(w.name)) {
    throw new Error(`worlds.ts: slug "${w.slug}" does not match toSlug("${w.name}")`)
  }
}

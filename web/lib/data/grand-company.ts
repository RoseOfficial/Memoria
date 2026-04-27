// FFXIV's three Grand Companies. Lodestone IDs 1–3; 0 = unaffiliated.

export type GrandCompany = {
  label: string
  short: string
  color: string  // CSS color for the chip border/text
}

const TABLE: Record<number, GrandCompany> = {
  1: { label: "Maelstrom", short: "GLM", color: "var(--color-gc-maelstrom, #d04b4b)" },
  2: { label: "Order of the Twin Adder", short: "OTA", color: "var(--color-gc-twin-adder, #d4ad3a)" },
  3: { label: "Immortal Flames", short: "IMF", color: "var(--color-gc-immortal, #c87d3a)" },
}

export function resolveGrandCompany(id: number | null | undefined): GrandCompany | null {
  if (id == null || id === 0) return null
  return TABLE[id] ?? null
}

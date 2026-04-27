import { describe, it, expect } from "vitest"
import { resolveGrandCompany } from "../../lib/data/grand-company"

describe("resolveGrandCompany", () => {
  it("returns null for null/zero/unknown", () => {
    expect(resolveGrandCompany(null)).toBeNull()
    expect(resolveGrandCompany(0)).toBeNull()
    expect(resolveGrandCompany(99)).toBeNull()
  })

  it("resolves the three GCs", () => {
    expect(resolveGrandCompany(1)?.label).toBe("Maelstrom")
    expect(resolveGrandCompany(2)?.label).toBe("Order of the Twin Adder")
    expect(resolveGrandCompany(3)?.label).toBe("Immortal Flames")
  })
})

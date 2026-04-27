import { describe, it, expect } from "vitest"
import { resolveOnlineStatus } from "../../lib/data/online-status"

describe("resolveOnlineStatus", () => {
  it("returns null for null/zero", () => {
    expect(resolveOnlineStatus(null)).toBeNull()
    expect(resolveOnlineStatus(0)).toBeNull()
    expect(resolveOnlineStatus(undefined)).toBeNull()
  })

  it("resolves Sprout to New Adventurer", () => {
    expect(resolveOnlineStatus(27)).toEqual({ label: "New Adventurer", key: "sprout" })
  })

  it("resolves all mentor variants to mentor key", () => {
    expect(resolveOnlineStatus(29)?.key).toBe("mentor")
    expect(resolveOnlineStatus(30)?.key).toBe("mentor")
    expect(resolveOnlineStatus(31)?.key).toBe("mentor")
    expect(resolveOnlineStatus(32)?.key).toBe("mentor")
  })

  it("falls back to Online for unknown ids", () => {
    expect(resolveOnlineStatus(999)).toEqual({ label: "Online", key: "online" })
  })
})

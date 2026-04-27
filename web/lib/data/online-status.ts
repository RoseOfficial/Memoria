// Maps FFXIV OnlineStatus row IDs to human-readable labels and a key
// the UI uses to pick an icon/color. Source: Lumina OnlineStatus excel sheet.
// New patches occasionally add new statuses — when one shows up that we don't
// recognize, the UI falls back to "Online".
//
// We render only a small subset that's worth visualizing on a profile:
// Mentor variants, Sprout, Returner, AFK, Busy, Roleplaying. Everything else
// renders as the default "Online" dot.

export type OnlineStatusKey =
  | "online"
  | "busy"
  | "afk"
  | "rp"
  | "lfp"
  | "sprout"
  | "returner"
  | "mentor"

export type OnlineStatus = {
  label: string
  key: OnlineStatusKey
}

const TABLE: Record<number, OnlineStatus> = {
  1: { label: "Online", key: "online" },
  12: { label: "Busy", key: "busy" },
  17: { label: "Away", key: "afk" },
  21: { label: "Looking to Meld Materia", key: "lfp" },
  22: { label: "Roleplaying", key: "rp" },
  23: { label: "Looking for Party", key: "lfp" },
  27: { label: "New Adventurer", key: "sprout" },
  28: { label: "Returner", key: "returner" },
  29: { label: "Mentor", key: "mentor" },
  30: { label: "PvE Mentor", key: "mentor" },
  31: { label: "Trade Mentor", key: "mentor" },
  32: { label: "PvP Mentor", key: "mentor" },
}

export function resolveOnlineStatus(id: number | null | undefined): OnlineStatus | null {
  if (id == null || id === 0) return null
  return TABLE[id] ?? { label: "Online", key: "online" }
}

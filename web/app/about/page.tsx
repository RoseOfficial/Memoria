export const metadata = { title: 'About — Memoria' }

export default function About() {
  return (
    <main className="max-w-2xl mx-auto px-8 py-12 space-y-6 prose-invert">
      <h1 className="text-3xl">About</h1>
      <p className="text-[var(--color-text-muted)]">
        Memoria is a FFXIV player lookup tool. A Dalamud plugin scans the players you encounter in-game;
        this website is where the data becomes searchable, browsable, and — if you claim your characters —
        yours to manage.
      </p>
      <p className="text-[var(--color-text-muted)]">
        Anyone can see public profile sections. Members of our Discord community can see richer data
        (locations, name/world history, alt characters). Owners can claim their character via a bio code
        and control what's shown.
      </p>
      <h2 className="text-xl mt-8">Contributing data</h2>
      <p className="text-[var(--color-text-muted)]">
        Install the plugin via XIVLauncher's custom plugin repo. Every player you walk past enriches
        the dataset for everyone. No scans, no data.
      </p>
    </main>
  )
}
